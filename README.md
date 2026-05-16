# ScalarGui

A Windows desktop application for managing large Git repositories using [Scalar](https://github.com/microsoft/scalar) and [sparse-checkout](https://git-scm.com/docs/git-sparse-checkout). Built with WPF and .NET 8.

## Why ScalarGui?

Working with monorepos or very large Git repositories can be painful — cloning takes forever, and you end up downloading files you don't need. Scalar and Git sparse-checkout solve this, but they're command-line tools with many flags to remember.

**ScalarGui** wraps these tools in a simple, guided UI so you can:

- Clone huge repos efficiently via Scalar
- Pick only the folders you need with a visual tree browser
- Skip the CLI entirely

## Features

### 🔧 Prerequisites Check

On first launch, ScalarGui verifies that **Git** and **Scalar** are installed. If either is missing, it shows download links and won't let you proceed until everything is ready.

### 📥 Scalar Clone

Clone repositories using `scalar clone` with a full set of options:

- **Branch selection** — clone a specific branch
- **Single branch / No tags** — minimize what gets downloaded
- **Full clone** — opt out of partial clone if needed
- **No src/** — skip Scalar's default `src/` directory wrapper
- **GVFS protocol** — for Azure DevOps repos that support it
- **Duplicate detection** — if the target directory already has a `.git` folder, ScalarGui warns you and offers to open the existing repo instead of cloning again

### 📂 Sparse-Checkout Browser

After cloning, browse the repo's directory tree and select only the folders you want to work with:

- Visual tree with expand/collapse
- Add or remove folders from the sparse-checkout set
- Real-time log output showing what Git is doing

### 💾 Remembers Your Inputs

ScalarGui persists your last-used:

- Repository URL
- Clone target directory
- Last opened repo path

So you don't have to re-type everything when you reopen the app.

### ⚙ Settings

Configure custom paths for `git` and `scalar` executables if they're not on your system PATH.

## Getting Started

### Prerequisites

| Tool | Version | Download |
|------|---------|----------|
| .NET 8 Runtime (Desktop) | 8.0+ | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0) |
| Git | 2.25+ | [git-scm.com](https://git-scm.com/downloads) |
| Scalar | Latest | [github.com/microsoft/scalar](https://github.com/microsoft/scalar/releases) |

> **Note:** Git 2.25+ is required for sparse-checkout support. Scalar is bundled with recent versions of [Git for Windows](https://gitforwindows.org/).

### Build from Source

```bash
git clone [<repository-url>](https://github.com/JayWang0/ScalarGui.git)
cd ScalarGui
dotnet build ScalarGui.slnx or dotnet run -project .\ScalarGui.csproj
```

### Run

```bash
dotnet run --project src/ScalarGui
```

Or open `ScalarGui.slnx` in Visual Studio 2022+ and press F5.

## Project Structure

```
ScalarGui/
├── ScalarGui.slnx              # Solution file
└── src/ScalarGui/
    ├── Models/                  # Data models (GitConfig, AppSettings)
    ├── Services/                # Git, Scalar, sparse-checkout, persistence services
    ├── ViewModels/              # MVVM ViewModels (CommunityToolkit.Mvvm)
    │   ├── SetupViewModel.cs    # Prerequisites detection
    │   ├── CloneViewModel.cs    # Clone workflow + input persistence
    │   ├── SparseCheckoutViewModel.cs  # Tree browser + folder selection
    │   ├── SettingsViewModel.cs # Tool path configuration
    │   └── MainViewModel.cs     # Navigation orchestrator
    ├── Views/                   # WPF XAML views
    ├── Converters/              # Value converters
    └── app.ico                  # Application icon
```

## Tech Stack

- **WPF** (.NET 8) — desktop UI framework
- **CommunityToolkit.Mvvm** — MVVM source generators (`[ObservableProperty]`, `[RelayCommand]`)
- **Scalar CLI** — efficient large-repo cloning
- **Git CLI** — sparse-checkout and tree operations

## Usage Workflow

1. **Setup** — App checks that Git and Scalar are installed
2. **Clone** — Enter a repo URL, pick a target directory, configure options, and clone
3. **Sparse-Checkout** — Browse the repo tree and select only the folders you need
4. **Work** — Open the repo in your editor with only the files you selected

## Screenshots

### Clone repository

![Clone repository](<Screenshots/clone repo.png>)

### Choose sparse-checkout folders

![Choose sparse-checkout folders](<Screenshots/checkout folders.png>)

## License
 
ScalarGui is licensed under the [MIT License](LICENSE).
