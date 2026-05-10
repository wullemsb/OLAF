using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OLAF.Configuration;
using OLAF.Extensions;
using OLAF.Tools;

var configuration = new ConfigurationBuilder()
	.AddJsonFile("appsettings.json", optional: false)
	.AddJsonFile("appsettings.Development.json", optional: true)
	.AddEnvironmentVariables("COPILOT_")
	.Build();

var config = configuration
	.GetSection(CopilotConfig.SectionName)
	.Get<CopilotConfig>() ?? new CopilotConfig();

var skillDirectories = ResolveSkillDirectories(AppContext.BaseDirectory, config);

var extensionLoader = new ExtensionToolLoader(AppContext.BaseDirectory, config);

var clientOptions = new CopilotClientOptions();
if (!string.IsNullOrWhiteSpace(config.GitHubToken))
{
	clientOptions.GitHubToken = config.GitHubToken;
}

//Change the Copilot home directory to define where session state is stored.
clientOptions.CopilotHome= Path.Combine(AppContext.BaseDirectory, "copilot-home");

await using var client = new CopilotClient(clientOptions);
await client.StartAsync();

CopilotSession? session = null;
ExtensionToolLoadResult? currentExtensionLoad = null;
bool safeMode = false;

await StartNewSessionAsync();

Console.Title = "OLAF";
PrintBanner();
PrintHelp();

while (true)
{
	PrintPrompt();

	var input = Console.ReadLine()?.Trim();
	if (string.IsNullOrWhiteSpace(input))
	{
		continue;
	}

	if (input.StartsWith('/'))
	{
		var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
		var command = parts[0].ToLowerInvariant();
		var argument = parts.Length > 1 ? parts[1].Trim() : string.Empty;

		switch (command)
		{
			case "/exit":
			case "/quit":
				if (session is not null)
				{
					await session.DisposeAsync();
				}

				return;

			case "/clear":
				await StartNewSessionAsync();
				Console.WriteLine("[New session started]");
				continue;

			case "/start":
				if (string.IsNullOrWhiteSpace(argument))
				{
					Console.WriteLine($"[Current session ID: {session?.SessionId ?? "(none)"}]");
					Console.WriteLine("Usage: /start <session-id>  — start a new named session");
				}
				else
				{
					await StartNewSessionAsync(argument);
					Console.WriteLine($"[Named session started: {session!.SessionId}]");
				}
				continue;

			case "/resume":
				if (string.IsNullOrWhiteSpace(argument))
				{
					Console.WriteLine("Usage: /resume <session-id>");
				}
				else
				{
					await ResumeNamedSessionAsync(argument);
				}
				continue;

			case "/sessions":
				var sessions = await client.ListSessionsAsync();
				var sessionList = sessions.ToList();
				if (sessionList.Count == 0)
				{
					Console.WriteLine("[No saved sessions found]");
				}
				else
				{
					Console.WriteLine("[Saved sessions]");
					foreach (var s in sessionList)
					{
						var marker = s.SessionId == session?.SessionId ? " (current)" : string.Empty;
						Console.WriteLine($"  {s.SessionId}{marker}");
					}
				}
				continue;

			case "/delete":
				if (string.IsNullOrWhiteSpace(argument))
				{
					Console.WriteLine("Usage: /delete <session-id>");
				}
				else
				{
					if (argument == session?.SessionId)
					{
						await StartNewSessionAsync();
						Console.WriteLine("[New session started]");
					}
					await client.DeleteSessionAsync(argument);
					Console.ForegroundColor = ConsoleColor.DarkGray;
					Console.WriteLine($"[Session '{argument}' deleted]");
					Console.ResetColor();
				}
				continue;

			case "/safemode":
			    await StartNewSessionAsync();
				safeMode = !safeMode;
				Console.Title = safeMode ? "OLAF [SAFE MODE]" : "OLAF";
				PrintSafeModeBanner();
				continue;

			case "/help":
				PrintHelp();
				continue;

			default:
				Console.WriteLine($"Unknown command: {input}");
				continue;
		}
	}

	try
	{
		await SendMessageAsync(session!, input);
	}
	catch (Exception ex)
	{
		Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine($"[Error] {ex.Message}");
		Console.ResetColor();
	}
}

async Task StartNewSessionAsync(string? sessionId = null)
{
	if (session is not null)
	{
		await session.DisposeAsync();
	}

	currentExtensionLoad?.Dispose();
	currentExtensionLoad = extensionLoader.LoadTools();
	PrintExtensionLoadMessages(currentExtensionLoad);

	var tools = CreateBuiltInTools();
	tools.AddRange(currentExtensionLoad.Tools);

	var sessionConfig = new SessionConfig
	{
		Model = config.Model,
		Streaming = true,
		OnPermissionRequest = HandlePermissionRequestAsync,
		SystemMessage = new SystemMessageConfig
		{
			Mode = SystemMessageMode.Append,
			Content = config.SystemPrompt
		},
		InfiniteSessions = new InfiniteSessionConfig {
           Enabled = true, //Enabled by default
           BackgroundCompactionThreshold = 0.8,
           BufferExhaustionThreshold=0.9
        },
		Tools = tools,
		SkillDirectories = skillDirectories,
		Hooks = new SessionHooks
		{
			OnPreToolUse = HandlePreToolUseAsync
		}
	};

	if (!string.IsNullOrWhiteSpace(sessionId))
	{
		sessionConfig.SessionId = sessionId;
	}

	session = await client.CreateSessionAsync(sessionConfig);
}

async Task ResumeNamedSessionAsync(string sessionId)
{
	if (session is not null)
	{
		await session.DisposeAsync();
	}

	currentExtensionLoad?.Dispose();
	currentExtensionLoad = extensionLoader.LoadTools();
	PrintExtensionLoadMessages(currentExtensionLoad);

	var tools = CreateBuiltInTools();
	tools.AddRange(currentExtensionLoad.Tools);

	try
	{
		session = await client.ResumeSessionAsync(sessionId, new ResumeSessionConfig
		{
			OnPermissionRequest = HandlePermissionRequestAsync,
			Streaming = true,
			Tools = tools,
			SkillDirectories = skillDirectories,
			Hooks = new SessionHooks
			{
				OnPreToolUse = HandlePreToolUseAsync
			}
		});
		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.WriteLine($"[Resumed session: {session.SessionId}]");
		Console.ResetColor();
	}
	catch (Exception ex)
	{
		Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine($"[Error] Could not resume session '{sessionId}': {ex.Message}");
		Console.ResetColor();
		await StartNewSessionAsync();
		Console.WriteLine("[Started a new session instead]");
	}
}

async Task<PermissionRequestResult> HandlePermissionRequestAsync(PermissionRequest request, PermissionInvocation invocation)
{
	if (!safeMode)
		return await PermissionHandler.ApproveAll(request, invocation);

	Console.ForegroundColor = ConsoleColor.Yellow;
	Console.WriteLine();
	var description = request switch
	{
		PermissionRequestShell shell => $"shell command: {shell.FullCommandText}",
		PermissionRequestWrite write => $"write file: {write.FileName}",
		PermissionRequestRead read => $"read path: {read.Path}",
		PermissionRequestCustomTool tool => $"custom tool: {tool.ToolName}",
		PermissionRequestMcp mcp => $"MCP tool: {mcp.ToolName} ({mcp.ServerName})",
		PermissionRequestUrl url => $"fetch URL: {url.Url}",
		_ => $"operation ({request.Kind})"
	};
	Console.Write($"[Safe Mode] Allow {description}? (y/n): ");
	Console.ResetColor();

	var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
	return new PermissionRequestResult
	{
		Kind = answer == "y"
			? PermissionRequestResultKind.Approved
			: PermissionRequestResultKind.Rejected
	};
}

async Task<PreToolUseHookOutput?> HandlePreToolUseAsync(PreToolUseHookInput input, HookInvocation invocation)
{
	Console.WriteLine($"[PreToolUseHook] Tool: {input.ToolName}, Args: {input.ToolArgs}");
	if (!safeMode)
		return new PreToolUseHookOutput { PermissionDecision = "allow" };

	Console.ForegroundColor = ConsoleColor.Magenta;
	Console.WriteLine();
	Console.WriteLine($"[Safe Mode] Tool: {input.ToolName}");
	if (input.ToolArgs is not null)
		Console.WriteLine($"            Args: {input.ToolArgs}");
	Console.Write("Allow? (y/n): ");
	Console.ResetColor();

	var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
	bool allowed = answer == "y";
	return new PreToolUseHookOutput
	{
		PermissionDecision = allowed ? "allow" : "deny",
		PermissionDecisionReason = allowed ? "Approved by user" : "Denied by user"
	};
}

void PrintPrompt()
{
	if (safeMode)
	{
		Console.ForegroundColor = ConsoleColor.Red;
		Console.Write("[SAFE] > ");
	}
	else
	{
		Console.ForegroundColor = ConsoleColor.Green;
		Console.Write("> ");
	}
	Console.ResetColor();
}

void PrintSafeModeBanner()
{
	Console.WriteLine();
	if (safeMode)
	{
		Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine("  ╔══════════════════════════════════╗");
		Console.WriteLine("  ║      ⚠   SAFE MODE ON   ⚠       ║");
		Console.WriteLine("  ║  Tools require your approval     ║");
		Console.WriteLine("  ╚══════════════════════════════════╝");
	}
	else
	{
		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.WriteLine("  [ Safe mode OFF — tools auto-approved ]");
	}
	Console.ResetColor();
	Console.WriteLine();
}

static List<string> ResolveSkillDirectories(string applicationBaseDirectory, CopilotConfig config)
{
	if (!config.EnableSkills)
	{
		return new List<string>();
	}

	var path = Path.IsPathRooted(config.SkillsDirectory)
		? Path.GetFullPath(config.SkillsDirectory)
		: Path.GetFullPath(Path.Combine(applicationBaseDirectory, config.SkillsDirectory));

	return Directory.Exists(path) ? new List<string> { path } : new List<string>();
}

static List<AIFunction> CreateBuiltInTools()
{
	var fsTools = new FileSystemTools();

	return
	[
		AIFunctionFactory.Create(fsTools.ReadFile, name: "read_file"),
		AIFunctionFactory.Create(fsTools.ListFiles, name: "list_files"),
		AIFunctionFactory.Create(fsTools.GetCurrentDirectory, name: "get_current_directory"),
	];
}

async Task SendMessageAsync(CopilotSession copilotSession, string prompt)
{
	var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

	using var subscription = copilotSession.On(evt =>
	{
		switch (evt)
		{
			case AssistantMessageDeltaEvent delta:
				Console.Write(delta.Data.DeltaContent);
				break;

			case ToolExecutionStartEvent toolStart:
				Console.ForegroundColor = safeMode ? ConsoleColor.Red : ConsoleColor.Yellow;
				Console.WriteLine();
				Console.WriteLine($"[Tool: {toolStart.Data.ToolName}({toolStart.Data.Arguments})]");
				Console.ResetColor();
				break;

			case SessionIdleEvent:
				Console.WriteLine();
				tcs.TrySetResult(true);
				break;

			case SessionErrorEvent err:
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine();
				Console.WriteLine($"[Error] {err.Data.ErrorType}: {err.Data.Message}");
				Console.ResetColor();
				tcs.TrySetException(new InvalidOperationException(err.Data.Message));
				break;
		}
	});

	await copilotSession.SendAsync(new MessageOptions { Prompt = prompt });
	await tcs.Task;
}

static void PrintBanner()
{
	
	Console.WriteLine("  ___   _        _    _____ ");
	Console.WriteLine(@"/ _ \ | |      / \  |  ___|");
	Console.WriteLine(@"| | | | |     / _ \ | |_");
	Console.WriteLine(@"| |_| | |___ / ___ \|  _|  ");
	Console.WriteLine(@" \___/|_____/_/   \_\_| ");
	Console.WriteLine();
	Console.WriteLine("      _===_");
	Console.WriteLine("     (.,.)");
	Console.WriteLine("     ( : )");
	Console.WriteLine("    ( : : )");
	Console.WriteLine("   ( : : : )");
	Console.WriteLine();
	Console.WriteLine("AI CLI Developer Tool powered by GitHub Copilot SDK");
	Console.WriteLine();
}

static void PrintHelp()
{
	Console.WriteLine("Type your coding question or command. Type /help for options.");
	Console.WriteLine();
	Console.WriteLine("/help              Show available commands");
	Console.WriteLine("/clear             Start a fresh session");
	Console.WriteLine("/start <id>         Start a new named session (resumable later)");
	Console.WriteLine("/sessions          List all saved sessions");
	Console.WriteLine("/resume <id>       Resume a previously saved session");
	Console.WriteLine("/delete <id>       Permanently delete a session");
	Console.WriteLine("/safemode          Toggle safe mode (prompts for approval before each tool call)");
	Console.WriteLine("/exit              Quit the application");
	Console.WriteLine();
}

static void PrintExtensionLoadMessages(ExtensionToolLoadResult loadResult)
{
	foreach (var message in loadResult.Messages)
	{
		Console.ForegroundColor = message.Severity switch
		{
			ExtensionLoadSeverity.Error => ConsoleColor.Red,
			ExtensionLoadSeverity.Warning => ConsoleColor.Yellow,
			_ => ConsoleColor.DarkGray
		};

		Console.WriteLine($"[Extensions] {message.Message}");
		Console.ResetColor();
	}

	if (loadResult.ExtensionCount > 0)
	{
		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.WriteLine($"[Extensions] Loaded {loadResult.ExtensionCount} extension(s) exposing {loadResult.Tools.Count} tool(s).");
		Console.ResetColor();
	}
}
