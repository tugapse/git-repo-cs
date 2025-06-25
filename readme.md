# GitRepoPy: Python Project Manager

A cross-platform .NET Core console application designed to automate the setup, management, and removal of Python projects from Git repositories, simplifying development workflows.

---

## 📚 Table of Contents  
1. [Introduction](#introduction)  
2. [Features](#features)  
3. [Installation](#installation)  
4. [Usage](#usage)  
5. [Configuration](#configuration)  
6. [Project Structure](#project-structure)  
7. [Troubleshooting](#troubleshooting)  
8. [Contributing](#contributing)  
9. [License](#license)  
10. [Acknowledgements](#acknowledgements)  

---

## 📌 Introduction  
Setting up new Python projects from Git repositories often involves repetitive tasks like cloning, creating virtual environments, installing dependencies, and configuring execution scripts. GitRepoPy streamlines this workflow by automating these steps, ensuring consistency and efficiency across development environments.

---

## 🔧 Features  
- **Setup Mode**:  
  - Clones a Git repository to a designated directory.  
  - Creates a Python virtual environment.  
  - Installs dependencies from `requirements.txt` or executes `build.sh`.  
  - Generates a wrapper script for execution (`run.sh` or `main.py`).  

- **Removal Mode**:  
  - Safely deletes the project directory and its executable symlink.  

- **Update Mode**:  
  - Cleans `__pycache__` directories, stashes changes, pulls updates, and reapplies stashed changes.  

- **Force Create Run Script Mode**:  
  - Always generates a Python wrapper script, even if `run.sh` exists.  

- **Help Mode**:  
  - Displays usage instructions and options.  

---

## 📦 Installation  

### Prerequisites  
- [.NET SDK (8.0+)](https://dotnet.microsoft.com/download)  
- [Git](https://git-scm.com/download)  
- [Python 3](https://www.python.org/downloads/) with `venv` support  

---

### Building from Source  
1. Clone the repository:  
```bash
 git clone https://github.com/tugapse/git-repo-cs.git
   cd git-repo-cs
```
  Build the project:  
```bash
 dotnet build
```
  Publish a standalone executable:  
```bash
 dotnet publish -c Release -r win-x64 --self-contained true -o ./publish/win-x64
```
   (Repeat for Linux/macOS as needed)  

---

## 🚀 Usage  

### General Syntax  
```bash
git-repopy [OPTIONS] <repository_name> <github_url>
```

### Setup Mode  
```bash
git-repopy <repository_name> <github_url>
```
**Example**:  
```bash
git-repopy my-web-app https://github.com/myuser/my-web-app.git
```

### Removal Mode  
```bash
git-repopy --remove <repository_name>
```
**Example**:  
```bash
git-repopy --remove my-web-app
```

### Update Mode  
```bash
git-repopy --update <repository_name>
```
**Example**:  
```bash
git-repopy --update my-web-app
```

### Force Create Run Script Mode  
```bash
git-repopy --force-create-run <repository_name> <github_url>
```
**Example**:  
```bash
git-repopy --force-create-run my-api https://github.com/anotheruser/my-api.git
```

### Help Mode  
```bash
git-repopy --help
```

---

## 📁 Configuration  

### Environment Variables  
Customize base directories by setting these variables:  
- `TOOLS_BASE_DIR`: Root directory for cloned repositories.  
  - **Default (Windows)**: `C:\Users\<Username>\Tools`  
  - **Default (Linux/macOS)**: `/usr/local/tools`  
- `TOOLS_BIN_DIR`: Directory for project executables/symlinks.  
  - **Default (Windows)**: `C:\Users\<Username>\Tools\Bin`  
  - **Default (Linux/macOS)**: `/usr/local/bin`  

**Example (Linux/macOS)**:  
```bash
TOOLS_BASE_DIR=~/my_dev_tools TOOLS_BIN_DIR=~/bin git-repopy my-project https://github.com/user/my-project.git
```

**Example (Windows PowerShell)**:  
```powershell
$env:TOOLS_BASE_DIR="C:\DevTools"; $env:TOOLS_BIN_DIR="C:\DevTools\Scripts"; .\publish\win-x64\git-repopy.exe my-project https://github.com/user/my-project.git
```

---

## 📂 Project Structure  
The C# project is organized into modular classes:  
- `Program.cs`: Entry point and task delegation.  
- `GlobalConfig.cs`: Constants, paths, and color configurations.  
- `Logger.cs`: Console output and logging.  
- `CommandExecutor.cs`: Shell command execution.  
- `FileSystemHelper.cs`: File/directory operations.  
- `PathEnvironmentManager.cs`: PATH environment variable management.  
- `CliParser.cs`: Command-line argument parsing.  
- `ProjectService.cs`: Core logic for project management.  

---

## ⚠️ Troubleshooting  

### 1. **"Access to the path is denied"**  
- Ensure no other processes (IDEs, file explorers) are using the project directory.  
- Restart terminal or machine if file locks persist.  

### 2. **"fatal: unable to access 'https://github.com/...'"**  
- Check internet connection and DNS settings.  
- Configure Git proxy if behind a proxy:  
```bash
git config --global http.proxy http://proxy.example.com:8080
```

### 3. **"Failed to create virtual environment"**  
- Install `python3-venv` on Linux:  
```bash
sudo apt install python3-venv
```

### 4. **"Failed to create symbolic link"**  
- On Linux/macOS, use `sudo` for system directories like `/usr/local/bin`.  
- On Windows, ensure the directory is user-owned (not system-wide).  

---

## 🤝 Contributing  
1. Fork the repository on GitHub.  
2. Clone your fork:  
```bash
 git clone https://github.com/YOUR_GITHUB_USER/YOUR_REPO_NAME.git
```
Create a new branch for your feature/fix.  
4. Commit changes with clear messages.  
5. Push to your fork and open a Pull Request.  

---

## 📄 License  
This project is licensed under the **MIT License**.  
You can use, modify, and distribute the software freely, provided you retain the license and copyright notice.  

---

## 🙌 Acknowledgements  
Inspired by the simplicity of Bash scripting and built with the cross-platform power of .NET Core.  

--- 

*For more details, refer to the [LICENSE](LICENSE) file or visit [OSI Approved Licenses: MIT License](https://opensource.org/licenses/MIT).*