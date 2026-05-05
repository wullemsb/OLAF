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
if (!string.IsNullOrWhiteSpace(config.GithubToken))
{
	clientOptions.GithubToken = config.GithubToken;
}

await using var client = new CopilotClient(clientOptions);
await client.StartAsync();

CopilotSession? session = null;
ExtensionToolLoadResult? currentExtensionLoad = null;
await StartNewSessionAsync();

PrintBanner();
PrintHelp();

while (true)
{
	Console.ForegroundColor = ConsoleColor.Green;
	Console.Write("> ");
	Console.ResetColor();

	var input = Console.ReadLine()?.Trim();
	if (string.IsNullOrWhiteSpace(input))
	{
		continue;
	}

	if (input.StartsWith('/'))
	{
		switch (input.ToLowerInvariant())
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

async Task StartNewSessionAsync()
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

	session = await client.CreateSessionAsync(new SessionConfig
	{
		Model = config.Model,
		Streaming = true,
		SystemMessage = new SystemMessageConfig
		{
			Mode = SystemMessageMode.Append,
			Content = config.SystemPrompt
		},
		Tools = tools,
		SkillDirectories = skillDirectories
	});
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

static async Task SendMessageAsync(CopilotSession copilotSession, string prompt)
{
	var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

	copilotSession.On(evt =>
	{
		switch (evt)
		{
			case AssistantMessageDeltaEvent delta:
				Console.Write(delta.Data.DeltaContent);
				break;

			case AssistantMessageEvent msg:
				Console.Write(msg.Data.Content);
				break;

			case ToolExecutionStartEvent toolStart:
				Console.ForegroundColor = ConsoleColor.Yellow;
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
	Console.WriteLine("/help  Show available commands");
	Console.WriteLine("/clear Start a fresh session");
	Console.WriteLine("/exit  Quit the application");
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
