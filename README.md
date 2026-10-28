# zpX

**zpX** is a modern, fast and free archive manager for Windows 10/11, inspired by 7-Zip and Telegram.

- Publisher: **Cengiz Kemeç**
- Website: https://cengizkemec.com.tr/
- GitHub: https://github.com/cengizkemec
- Repository: https://github.com/cengizkemec/zpX
- License: MIT

## Features

- Modern dark and light themes (Telegram-inspired), custom title bar
- Built-in file manager: browse drives and folders, copy, move, delete, rename, new folder, drag and drop
- Archive support: open, extract, test (integrity), create
- Formats: ZIP, 7Z, RAR, TAR, GZ, BZ2, XZ, ZST, CAB, ISO, MSI, NSIS, WIM, DMG, VHD/VHDX, and more (read)
- .zpx native format (ZIP container with manifest)
- Compression options: Store, Fastest, Fast, Normal, Maximum, Ultra; AES-256 password protection; encrypt file names
- SHA-256, MD5, SHA-1, SHA-512 hashing tools
- Properties dialog, archive test with progress
- Multilingual: English, Turkish, Spanish, French, German, Italian, Portuguese, Russian, Japanese, Chinese
- Explorer integration: right-click "Compress with zpX", "Extract here with zpX"
- Silent background operations for context-menu actions
- Inno Setup installer: choose install location, desktop and Start Menu shortcuts

## Download

Get the latest release from [Releases](https://github.com/cengizkemec/zpX/releases).

- zpX-Setup.exe - installer (location choice and desktop shortcut)
- zpX-Portable.zip - portable (requires .NET 9 Desktop Runtime)

## Requirements

- Windows 10/11 x64
- .NET 9 Desktop Runtime (portable build only)

## Build from Source

```bash
git clone https://github.com/cengizkemec/zpX.git
cd zpX
dotnet build zpX.slnx -c Release
```

## About

zpX is developed by Cengiz Kemeç.  
Website: https://cengizkemec.com.tr/  
GitHub: https://github.com/cengizkemec

This project is licensed under the MIT License.
