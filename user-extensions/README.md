# OLAF Runtime Extensions

Place trusted local `.cs` files in this folder to add tools to OLAF.

Rules for the first version:
- Implement `OLAF.Extensions.IOlafExtension`.
- Expose tools from `GetTools(...)` using `AIFunctionFactory.Create(...)`.
- Keep dependencies limited to assemblies already loaded by OLAF.
- Reload extensions by starting OLAF or by using `/clear` inside a running session.