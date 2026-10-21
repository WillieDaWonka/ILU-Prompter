# ILU Prompter

A Windows teleprompter for ILUMINA. One control window for the operator, and one borderless, fullscreen,
non-mirrored output on a second display. Scroll with a mouse wheel (optionally a dedicated second mouse), an
Xbox controller, or a Contour ShuttleXpress / ShuttlePRO jog-shuttle.

WPF on .NET 8 (`net8.0-windows`), self-contained folder build, no installer, no admin.

## Running it

- **Just want to run it?** Download `ILUPrompter-<version>-win-x64.zip` from
  [Releases](https://github.com/WillieDaWonka/ILU-Prompter/releases), unzip it anywhere and start `ILUPrompter.exe`
  (no installer, no admin, no .NET install needed).
- **Double-click `run_app.bat`.** It builds a Release copy and starts it. With a second display extended, press
  **F11** (or *Show output*) to put the prompt on it.
- **Only one screen?** Double-click `run_app_single_screen.bat`; the output opens as a window on this screen.
- **Visual Studio:** open `IluPrompter.sln`, right-click **IluPrompter.App** › *Set as Startup Project*, press F5.
- **For a show machine:** run the publish command below and copy the `publish\ILUPrompter` folder; start
  `ILUPrompter.exe` from it (no .NET install needed).

## Build, test, run

```powershell
dotnet build IluPrompter.sln
dotnet test IluPrompter.sln
dotnet run --project src/IluPrompter.App -- samples/sample-short.txt
dotnet publish src/IluPrompter.App -c Release -p:SelfContained=true -o publish/ILUPrompter
```

Dev flags: `--diag` (frame timing + input trace in the log), `--dev-output-on-primary` (output as a 960 × 540
window when there is no second display), `--settings-file <path>` (throwaway settings), `--window-size WxH`.

Settings: `%APPDATA%\ILU Prompter\settings.json`. Logs: `%LOCALAPPDATA%\ILU Prompter\logs\`.

## What it does

- Script editor: bold, italic, underline, strikethrough, 8 inks, 5 highlights, 4 sizes; a paragraph starting `##`
  is a section. Opens `.ilup`, `.txt`, `.rtf`, `.docx`; exports `.rtf`, `.txt`.
- Output layout: lines visible, line spacing, margins left/right/top/bottom with a link toggle, reading line.
- Live preview of the output at true proportions, with margins and the reading line drawn to scale (drag to adjust).
- Transport: play/pause, speed −10…10 (≈ wpm shown), display scrolling vs line scrolling, previous/next line,
  paragraph and section, jump to start, blank output. Jumps land the text on the reading line.

## Docs

| File | What |
|---|---|
| [docs/RESEARCH.md](docs/RESEARCH.md) | Framework, input, rendering and storage research |
| [DESIGN.md](DESIGN.md) | Visual and wording system (house style from venue-monitoring) |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Build contract: projects, contracts, threading, streams, Phase-0 checks |
| [docs/streams/](docs/streams/) | Per-stream notes, integration results and screenshots |
