# Installation & Setup Guide

This guide will walk you through installing and setting up the Deterministic Game Framework in your project.

## Prerequisites

### Required Software
- **.NET 8.0 SDK** or later
- **Git** (for submodule management)
- **IDE**: Visual Studio 2022, Rider, or VS Code with C# extensions

### Framework Dependencies
The framework automatically manages these NuGet packages:
- `Newtonsoft.Json` (13.0.3) - JSON serialization
- `Microsoft.AspNetCore.SignalR.Core` (1.1.0) - Server networking
- `Microsoft.AspNetCore.SignalR.Client` (8.0.0) - Client networking
- `R3` (1.2.15+) - Reactive extensions for client
- `RestSharp` (112.1.0) - REST API client
- `System.Collections.Immutable` (8.0.0) - Immutable collections
- `JetBrains.Annotations` (2025.2.2) - Code annotations

## Installation Methods

### Method 1: Git Submodule (Recommended)

This method keeps the framework as a separate repository that can be updated independently.

#### Step 1: Add Submodule
```bash
# Navigate to your project's Server directory
cd YourProject/Server

# Add the framework as a submodule
git submodule add https://github.com/Sod3n/Determenistic.GameFramework.git Framework

# Initialize and update the submodule
git submodule update --init --recursive
```

#### Step 2: Reference Framework Projects
Add project references to your game's `.csproj` file:

```xml
<ItemGroup>
  <ProjectReference Include="..\Framework\Deterministic.GameFramework.Core\Deterministic.GameFramework.Core.csproj" />
  <ProjectReference Include="..\Framework\Deterministic.GameFramework.Server\Deterministic.GameFramework.Server.csproj" />
  <!-- For client projects, also add: -->
  <ProjectReference Include="..\Framework\Deterministic.GameFramework.Client\Deterministic.GameFramework.Client.csproj" />
</ItemGroup>
```

#### Step 3: Update Submodule (Future Updates)
```bash
# Pull latest framework changes
cd Framework
git pull origin main
cd ..

# Commit the submodule update
git add Framework
git commit -m "Update framework to latest version"
```

### Method 2: Direct Clone

For simpler projects or testing, you can clone directly into your project.

```bash
# Clone into your Server directory
cd YourProject/Server
git clone https://github.com/Sod3n/Determenistic.GameFramework.git Framework
```

**Note:** This method doesn't track the framework as a submodule, so updates require manual re-cloning.

### Method 3: Copy Projects

Copy the framework projects directly into your solution (not recommended for production).

1. Download or clone the repository
2. Copy the project folders into your solution
3. Add them to your `.sln` file

## Project Structure

After installation, your project structure should look like:

```
YourProject/
├── Server/
│   ├── Framework/                          # Framework submodule
│   │   ├── Deterministic.GameFramework.Core/
│   │   ├── Deterministic.GameFramework.Server/
│   │   ├── Deterministic.GameFramework.Client/
│   │   ├── Deterministic.GameFramework.SourceGenerators/
│   │   └── Deterministic.GameFramework.Examples/
│   ├── YourGame.Server/                    # Your server project
│   ├── YourGame.Shared/                    # Your shared logic
│   └── YourGame.Server.sln
└── Client/                                  # Your game client (Unity/Godot/etc)
```

## Verify Installation

### Build Test
```bash
# Build the framework
cd Framework
dotnet build Determenistic.GameFramework.sln

# Should output: Build succeeded
```

### Run Examples
```bash
# Run the examples project
cd Deterministic.GameFramework.Examples
dotnet run

# Or run specific examples:
dotnet run -- 1  # Hello World
dotnet run -- 2  # Reactions
dotnet run -- 3  # Domain Hierarchy
```

## Configuration

### Client DLL Auto-Copy (Optional)

If you're using Godot, Unity, or another game engine, you can configure automatic DLL copying:

In your game project's `.csproj`:

```xml
<PropertyGroup>
  <!-- Enable auto-copy to client -->
  <CopyToClientPlugins>true</CopyToClientPlugins>
  
  <!-- Set your client plugins path -->
  <ClientPluginsPath>$(ProjectDir)..\..\Client\plugins\Network\</ClientPluginsPath>
</PropertyGroup>

<ItemGroup>
  <ProjectReference Include="..\Framework\Deterministic.GameFramework.Client\Deterministic.GameFramework.Client.csproj" />
</ItemGroup>
```

### Source Generator Configuration

The framework includes source generators for creating enums from JSON data:

1. Create a `GameData` folder in your project
2. Add JSON files with your game data
3. Create a `SourceGeneratorConfig.json`:

```json
{
  "enums": [
    {
      "source": "Cards",
      "field": "card_id",
      "enumName": "CardId"
    },
    {
      "source": "Characters",
      "field": "character_id",
      "enumName": "CharacterId"
    }
  ]
}
```

4. Reference the source generator in your `.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="..\Framework\Deterministic.GameFramework.SourceGenerators\Deterministic.GameFramework.SourceGenerators.csproj" 
                    OutputItemType="Analyzer" 
                    ReferenceOutputAssembly="false" />
  <AdditionalFiles Include="GameData\**\*.json" />
</ItemGroup>
```

## Next Steps

Now that you have the framework installed, continue with:

1. **[Quick Start Tutorial](01-quick-start.md)** - Build your first deterministic game
2. **[Core Concepts](../getting-started/intro.md)** - Understand the framework architecture
3. **[Network Setup](../network/00-hello-world-multiplayer.md)** - Add multiplayer support

## Troubleshooting

### Build Errors

**Error: "The type or namespace name 'Deterministic' could not be found"**
- Ensure project references are correct in your `.csproj`
- Rebuild the framework: `dotnet build Framework/Determenistic.GameFramework.sln`

**Error: "Submodule not found"**
```bash
git submodule update --init --recursive
```

**Error: "NuGet package restore failed"**
```bash
dotnet restore
```

### Submodule Issues

**Submodule shows as modified but no changes**
```bash
cd Framework
git checkout main
cd ..
git add Framework
```

**Reset submodule to tracked commit**
```bash
git submodule update --force
```

## Support

- **GitHub Issues**: https://github.com/Sod3n/Determenistic.GameFramework/issues
- **Examples**: See `Framework/Deterministic.GameFramework.Examples/`
- **Documentation**: Browse the `articles/` folder in the Examples project
