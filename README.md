# zpX

A modern, minimal and fast archive tool inspired by Telegram. Built with C# + WPF on .NET 9.

## Features

- **Modern UI** - Clean, Telegram-inspired interface with custom frameless title bar
- **Dark / Light Themes** - Switch between themes instantly
- **X Accent Design** - Distinctive zpX logo with emphasized X`n- **Wide Archive Support** - Open ZIP, 7Z, RAR, TAR, GZ, BZ2, XZ, ZST, .zpx`n- **Drag & Drop** - Simply drag and drop archives into the window
- **Integrity Test** - Built-in archive health check
- **Multilingual** - Turkish, English, German, French, Spanish
- **Fast & Lightweight** - Powered by SharpCompress and ZstdSharp.Port

## Download

Get the latest release from [Releases](https://github.com/cengizkemec/zpX/releases).

**v0.2.0** (recommended): run `zpX-win-Setup.exe` — installs zpX with desktop and Start Menu shortcuts and updates itself.

Portable: `zpX-0.2.0-win-x64.zip` (extract and run `zpX.exe`; requires .NET 9 Desktop Runtime).

## Requirements

- Windows 10/11 x64
- [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) (portable build only; the installer bundles it)

## Build from Source

`ash

git clone https://github.com/cengizkemec/zpX.git
cd zpX
dotnet build zpX.slnx -c Release
` 

## License

This project is licensed under the [MIT License](LICENSE).
