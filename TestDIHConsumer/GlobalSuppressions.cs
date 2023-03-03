// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
	"Style",
	"IDE1006:Naming Styles",
	Justification = "Using JSON naming convention for documents",
	Scope = "module")]
[assembly: SuppressMessage(
	"Design",
	"CA1050:Declare types in namespaces",
	Justification = "Toplevel statements (like Program) is intentionally in the global namespace: https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/program-structure/top-level-statements#global-namespace",
	Scope = "type",
	Target = "~T:Program")]
[assembly: SuppressMessage(
	"Major Code Smell",
	"S1118:Utility classes should not have public constructors",
	Justification = "Program is not a utility class, and needs to be public for testing exposure",
	Scope = "type",
	Target = "~T:Program")]
[assembly: SuppressMessage(
	"Major Bug",
	"S3903:Types should be defined in named namespaces",
	Justification = "Toplevel statements (like Program) is intentionally in the global namespace: https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/program-structure/top-level-statements#global-namespace",
	Scope = "type",
	Target = "~T:Program")]

[ExcludeFromCodeCoverage]
internal partial class Program { }
