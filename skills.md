# Ben's Engineering Metrics - Development Skills & Standards

This document outlines the coding standards, architectural patterns, and constraints for this project. All AI assistants and developers should follow these guidelines.

## Project Overview

- **Language**: C# (.NET 10.0)
- **Build Configuration**: Debug mode enforces warnings as errors
- **IDE**: JetBrains Rider (ReSharper-compatible formatting)
- **Architecture**: Dependency Injection (Microsoft.Extensions.DependencyInjection)

## Code Style Requirements

### Field Access & Naming

- **Always prefix field access with `this.`**
  ```csharp
  // ✓ Correct
  if (this.allTasks.Count() > 0) { }
  var commandLineArgs = this.commandLineArgs;
  this.commandLineArgs = args;

  // ✗ Wrong
  if (allTasks.Count() > 0) { }
  var commandLineArgs = commandLineArgs;
  ```

- **No underscore prefix on fields** (no `_fieldName`)
  ```csharp
  // ✓ Correct
  private readonly IEngineeringMetricsTask[] allTasks;
  private string[] commandLineArgs;

  // ✗ Wrong
  private readonly IEngineeringMetricsTask[] _allTasks;
  private string[] _commandLineArgs;
  ```

### Naming Conventions

- **Interfaces**: PascalCase with `I` prefix `IEngineeringMetricsTask`, `IJiraQueryRunner`
- **Classes**: PascalCase `App`, `JiraApiClient`, `GoogleSheetUpdater`
- **Properties**: PascalCase `Description`, `Key`
- **Constants**: PascalCase `DefaultFolder` (no `_` prefix)
- **Private Fields**: camelCase (no `_` prefix) `allTasks`, `commandLineArgs`
- **Local Variables**: camelCase `mapper`, `factory`, `help`

### Expression-Bodied Members

- **Properties**: Use expression-bodied syntax (recommended)
  ```csharp
  public string Key => this.key;
  public bool IsValid => this.items.Any();
  ```

- **Methods**: Do NOT use expression-bodied syntax; use explicit blocks
  ```csharp
  // ✓ Correct
  public async Task ExecuteAsync(string[] args)
  {
      await this.DoWork();
  }

  // ✗ Wrong
  public async Task ExecuteAsync(string[] args) => await this.DoWork();
  ```

### Type Inference

- Use `var` for all local variables when type is apparent
  ```csharp
  var builder = Host.CreateDefaultBuilder();
  var client = new HttpClient();
  var index = 0;
  var result = await ServiceCall();
  ```

### Braces & Formatting

- Always use braces for control structures
  ```csharp
  // ✓ Correct
  if (mode == "NOT_SET")
  {
      Console.WriteLine(help);
  }

  // ✗ Wrong
  if (mode == "NOT_SET") Console.WriteLine(help);
  ```

- Brace placement: Always on new line
  ```csharp
  private async Task ExecuteMode(string? mode)
  {
      // content
  }
  ```

### Max Line Length
- **200 characters** (enforced in `.editorconfig`)

### Line Endings & Spacing
- **Line Endings**: CRLF (Windows)
- **Indent**: 4 spaces (no tabs)
- **Trailing Whitespace**: Always remove
- **End of File**: Always end with newline

## Testing Framework & Dependencies

### Approved Testing Libraries

- **Test Framework**: XUnit ONLY (`xunit`, `xunit.runner.visualstudio`)
- **Mocking Framework**: NSubstitute ONLY
- **Test Output**: `Xunit.Abstractions.ITestOutputHelper`

### Test Patterns

```csharp
using NSubstitute;
using Xunit;

public class MyTest
{
    [Fact]
    public async Task DescriptiveTestName()
    {
        // Arrange
        var mock = Substitute.For<IInterface>();
        var sut = new ClassUnderTest(mock);

        // Act
        var result = await sut.ExecuteAsync();

        // Assert
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("value1")]
    [InlineData("value2")]
    public void ParameterizedTest(string value)
    {
        // implementation
    }
}
```

### Substitute (NSubstitute) Usage

```csharp
var mockRepository = Substitute.For<IJiraIssueRepository>();
mockRepository.GetIssues(Arg.Any<string>()).Returns(new[] { issue });
```

## Architectural Patterns

### Dependency Injection

- Use constructor injection with primary constructor syntax (C# 12+)
  ```csharp
  public class App(IEnumerable<IEngineeringMetricsTask> tasks)
  {
      // Access injected dependency via 'tasks' parameter
      this.allTasks = tasks.ToArray();
  }
  ```

- Register in `Program.cs` using Microsoft.Extensions.DependencyInjection
  ```csharp
  services.AddSingleton<IJiraQueryRunner, JiraQueryDynamicRunner>();
  services.AddTransient<ICsvExporter, SimpleCsvExporter>();
  ```

### Interface-Based Design

- All major services should have interfaces prefixed with `I`
- Implementations should be injected via DI, not `new`

### Task Discovery Pattern

- Implement `IEngineeringMetricsTask` for discoverable tasks
- Tasks are auto-discovered at runtime via reflection
- No manual registration required if following the interface

```csharp
public interface IEngineeringMetricsTask
{
    string Key { get; }
    string Description { get; }
    Task ExecuteAsync(string[] args);
}
```

## Package Policy

### Approved Packages

- **Google APIs**: `Google.Apis.Auth`, `Google.Apis.Drive.v3`, `Google.Apis.Sheets.v4`
- **Microsoft Extensions**: `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Hosting`
- **Testing**: `NSubstitute`, `xunit`, `xunit.runner.visualstudio`
- **Framework**: `Microsoft.NET.Test.Sdk`, `coverlet.collector`

### Constraints

- **NO new NuGet packages** without architectural review and explicit approval
- **Rationale**: Keep dependencies minimal and stable; review before adding frameworks, logging libraries, or utilities
- Current toolset is sufficient for the application's needs

## Code Organization

### File Structure

```
BensEngineeringMetrics.Console/
├── Core/
│   ├── App.cs                    # Main application orchestrator
│   ├── Program.cs                # DI configuration and entry point
│   └── Constants.cs
├── Jira/                         # Jira API integration
│   ├── JiraApiClient.cs
│   ├── JiraIssueRepository.cs
│   └── BasicJiraTypes.cs
├── Google/                       # Google Sheets/Drive integration
│   ├── GoogleSheetReader.cs
│   ├── GoogleSheetUpdater.cs
│   └── AuthHelper.cs
├── Slack/                        # Slack integration
│   └── SlackClient.cs
├── Tasks/                        # Executable task implementations
│   └── [IEngineeringMetricsTask implementations]
└── Interfaces/                   # Service contracts
    ├── IJiraQueryRunner.cs
    ├── ICloudUploader.cs
    └── [other service interfaces]
```

### Namespace Usage

- File-scoped namespaces (C# 11+)
  ```csharp
  namespace BensEngineeringMetrics;
  ```

- Use sub-namespaces for organization
  ```csharp
  namespace BensEngineeringMetrics.Jira;
  namespace BensEngineeringMetrics.Google;
  namespace BensEngineeringMetrics.Tasks;
  ```

## Common Patterns & Anti-Patterns

### ✓ DO

- Use dependency injection for all major services
- Implement interfaces for all service contracts
- Use async/await for I/O operations
- Use `Substitute.For<T>()` for mocking in tests
- Prefix field access with `this.`
- Use pattern matching for null checks: `if (obj is null) { }`
- Use null-coalescing: `value ?? defaultValue`
- Use null-propagation: `obj?.Method()`

### ✗ DON'T

- Create instances with `new` outside of DI setup
- Use underscore prefix on field names
- Access fields without `this.` prefix
- Use `var` for non-obvious types (when reading the line doesn't immediately clarify type)
- Mix testing frameworks (only XUnit + NSubstitute)
- Add new NuGet packages without review
- Use expression-bodied methods (only for properties)
- Omit braces on control structures

## Detailed Configuration Reference

For specific formatting rules (indentation, line endings, spacing), see `.editorconfig` in the project root. This file is enforced by:
- JetBrains Rider / ReSharper
- Visual Studio
- GitHub Copilot and other AI tools

## AI Assistant Guidelines

When working with GitHub Copilot or other AI assistants on this codebase, follow these interaction patterns:

### Decision Making

- **Never guess or hallucinate** - If uncertain about code patterns, architectural decisions, or project-specific conventions, stop and ask the developer for clarification
- **Always provide options** - When there are multiple valid approaches, present at least two options with pros/cons before implementing
- **Default to existing patterns** - When unsure how to implement something, reference existing similar code in the project instead of inventing new patterns
- **Verify before generating** - Confirm understanding of requirements before writing code

### Code Generation

- **Follow skills.md first** - Generated code must comply with all standards in this document
- **Verify against .editorconfig** - Ensure formatting matches `.editorconfig` rules (line length, indentation, CRLF, etc.)
- **Check approved packages only** - Never suggest or add packages outside the approved list without developer approval
- **Use existing interfaces** - Prefer injecting existing interfaces over creating new ones
- **Match existing code style** - Generated code should be indistinguishable from existing code in the project

### Testing Support

- **XUnit + NSubstitute only** - Never suggest MSTest, NUnit, Moq, or other testing/mocking frameworks
- **Follow test patterns** - Generated tests must use Arrange-Act-Assert pattern with NSubstitute mocks as shown in this document
- **Validate test structure** - Tests should match patterns from existing test files in the project

### Architectural Questions

- **Ask before suggesting major changes** - Propositions to refactor, reorganize, or redesign should include context of why and present alternatives
- **Clarify requirements first** - Before generating code for a new feature/task, confirm understanding of requirements
- **Reference existing implementations** - Point to similar existing code before proposing new approaches
- **Explain the "why"** - When suggesting architectural patterns, explain why they align with the project's existing architecture

### Dependency & Package Questions

- **Escalate package requests** - If code generation would require a new package, stop and ask the developer to review the approved list first
- **Suggest workarounds with existing packages** - Try to accomplish goals with current approved packages before requesting new ones
- **Provide migration path** - If a new package is needed, explain what would need to change in the current code

### Field Access & Naming Issues

- **When in doubt about field names** - Ask the developer for the intended field name rather than inventing one that doesn't match conventions
- **Validate `this.` prefix usage** - Generated field access must always use `this.` prefix; flag any violations
- **Check naming rules** - New identifiers must follow naming conventions for the element type (interface, class, field, etc.)
- **Never use underscore prefix** - All field names must be camelCase without leading underscore

### Style & Formatting Conflicts

- **Default to .editorconfig** - When in doubt about code style, the `.editorconfig` rules are the source of truth
- **Ask about ambiguous conventions** - If a code pattern isn't clearly covered by `.editorconfig` or `skills.md`, ask the developer for clarification
- **Preserve existing formatting** - When modifying existing code, maintain the same formatting style

## Questions or Clarifications?

When uncertain about style or pattern decisions, refer to existing code in:
- `App.cs` - Main orchestration and DI patterns
- `Jira/JiraApiClient.cs` - Complex service implementation
- `BensEngineeringMetrics.Test/CalculatePmPlanReleaseBurnUpValuesTest.cs` - Test patterns

For architectural decisions, see the project `README.md`.
