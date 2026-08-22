<p align="center">
 <img width="150px" src="docs/logo_250.png" align="center" alt="DLSS Swapper" />
 <h2 align="center">DLSS Swapper
</h2>
 <p align="center">DLSS Swapper is a tool that allows you to conveniently download, manage, and swap <strong>DLSS</strong>, <strong>FSR</strong> and <strong>XeSS</strong> dlls allowing you to upgrade or downgrade DLSS, FSR and XeSS version in a game without the game needing an update.</p>
</p>

> [!IMPORTANT]
> **This is a personal fork of [beeradmoore/dlss-swapper](https://github.com/beeradmoore/dlss-swapper), not the original.**
> It carries a substantial interface redesign that has not been offered upstream — see
> [What is different in this fork](#what-is-different-in-this-fork) and [CHANGELOG.md](CHANGELOG.md).
> Builds here are **unsigned**, so Windows will warn before running one — this fork has no code
> signing certificate and does not claim to be the official DLSS Swapper. The signed, official
> build is [the original project's](https://github.com/beeradmoore/dlss-swapper/releases).
> Report anything wrong with the interface [on this fork](https://github.com/dkflint723/dlss-swapper/issues);
> anything else belongs upstream.

> [!WARNING]
> Please be aware of malicious sites claiming to be DLSS Swapper. See the original project's [official links](#official-links) for accounts and sites affiliated with DLSS Swapper. **This fork is not one of them.** It publishes unsigned builds from this repository only — nowhere else, and never as the official DLSS Swapper.

<p align="center">
    <a href="https://github.com/dkflint723/dlss-swapper/issues"><img alt="Issues on this fork" src="https://img.shields.io/github/issues/dkflint723/dlss-swapper?color=0088ff&label=fork%20issues" /></a>
    <a href="https://github.com/dkflint723/dlss-swapper/commits/main"><img alt="Last commit to this fork" src="https://img.shields.io/github/last-commit/dkflint723/dlss-swapper?label=fork%20updated" /></a>
    <a href="https://github.com/beeradmoore/dlss-swapper/releases"><img alt="Latest release of the original" src="https://img.shields.io/github/v/release/beeradmoore/dlss-swapper?label=original%20release" /></a>
</p>

<p align="center">
    <a href="https://github.com/beeradmoore/dlss-swapper/releases">Download the original</a>
    ·
    <a href="https://github.com/dkflint723/dlss-swapper/issues/new">Report something about this interface</a>
    ·
    <a href="https://github.com/beeradmoore/dlss-swapper/issues/new/choose">Everything else, upstream</a>
</p>

<p align="center">
    <a href="./readmes/readme_ca.md">Català</a>
    ·
    English
    ·
    <a href="./readmes/readme_es.md">Español</a>
    ·
    <a href="./readmes/readme_ja-JP.md">日本語</a>    
    ·
    <a href="./readmes/readme_pt-BR.md">Português BR</a>
    ·
    <a href="./readmes/readme_tr-TR.md">Türkçe</a>
    ·
    <a href="./readmes/readme_zh-Hans.md">简体中文</a>
    ·
    <a href="./readmes/readme_zh-TW.md">繁體中文</a>
</p>

<p align="center">
    <img src="docs/images/fork/games-page.png" alt="The games page in this fork: a sidebar with counts, filter tabs, and cards that each name their state and which upscalers the game has" />
</p>

## What is different in this fork

The app does the same job; it explains itself while doing it. Everything below replaced something
that worked but could only be read by someone who already knew what it meant. [CHANGELOG.md](CHANGELOG.md)
has the full list.

- **State is words, never colour.** Vendor-coloured badges are gone. Every game and every dll says
  what it is in a sentence, with a glyph beside it — the app is usable by someone who cannot tell
  red from green, which the badges assumed you could.
- **Nothing writes to a game without showing you what it will write.** Updating opens a sheet
  listing each file, and the strip that follows keeps what it wrote so it can put that batch back.
- **A game has its own page**, one row per upscaler, each saying which version is installed, whether
  anything newer exists, whether the original was kept, and whether the dll was found twice. It was
  nine dropdowns and eight unlabelled icon buttons.
- **The upscalers list is searchable**, grouped by release line, and says how many games use each
  file — the one question worth answering before deleting one, and you can click the number to see
  which games.
- **Counts agree with the lists they describe.** Several did not: "All games" counted hidden games it
  would not show, and the engine counts included debug files the list was hiding.
- **Every setting says what it does**, contrast is measured against the surface it lands on rather
  than eyeballed, and menus stay inside the window.

## What game libraries are supported?

- [Steam](https://store.steampowered.com/)
- [GOG](https://www.gog.com/en/)
- [Epic Games](https://store.epicgames.com/)
- [Ubisoft Connect](https://www.ubisoft.com/)
- [Xbox App](https://www.xbox.com/)
- [Battle.net](https://shop.battle.net/)
- Manually added via the `Add Game` button.

## Why would you want to change the DLSS dlls in your game?

See [this](https://youtube.com/clip/UgzYyeox3s7jFJZAvYF4AaABCQ) clip, or better yet just watch the entire video ([Lego Builder's Journey Ray Tracing Showcase + DLSS 2.2 Upgrades Analysis](https://www.youtube.com/watch?v=dtbqJXb1UDw)) from Digital Foundry. DLSS 2.2 discussions start at 11:40.

## Please note

This tool does **NOT** allow you to add DLSS to games that don't support it.

This tool does **NOT** guarantee that swapping DLSS dlls will:

- Improve DLSS performance.
- Reduce DLSS artifacts.
- Give a crash free experience.

In many cases you may fix some issues, in other cases you may prevent a game from launching (until you restore your original dll, provided in the tool).

Happy experimenting. As my university professor once said,

> The good thing about computer [science] is we will never die wondering 'What if...?'

Please, come and share your DLSS experience over in [r/DLSS_Swapper](https://www.reddit.com/r/DLSS_Swapper/).

## How do I get it?

**This fork publishes unsigned builds** on its [releases page](https://github.com/dkflint723/dlss-swapper/releases)
— an installer and a portable zip. They carry no certificate, so SmartScreen will warn on first run
and you will have to click through it. That is the cost of a fork without code signing, and it is a
good reason to prefer the original unless you specifically want this interface.

Or build it yourself with the .NET 10 SDK:

> dotnet build "src\DLSS Swapper.csproj" -c Release -p:Platform=x64

**The original** has proper releases, signed, on its [GitHub releases](https://github.com/beeradmoore/dlss-swapper/releases) page, or:

> winget install --id=beeradmoore.dlss-swapper -e

That winget package is the original project, not this fork. Those are the only official places to get
DLSS Swapper.

## It would be cool if DLSS Swapper could...

Create a [feature request](https://github.com/beeradmoore/dlss-swapper/issues/new?template=feature_request.yml).

## How can I contribute?

More info on this soon.

## Minimum System Requirements

| Requirement | Description                           |
| ----------- | ------------------------------------- |
| OS          | Windows 10 64-bit (20H1, build 19041) |
| GPU         | Any                                   |

## Official links

Of the original project, which is where everything except this fork's interface work belongs:

- GitHub: https://github.com/beeradmoore/dlss-swapper/
- Twitter: https://twitter.com/dlss_swapper
- Reddit: https://www.reddit.com/r/DLSS_Swapper/

If you have found an other accounts or sites claiming to be DLSS Swapper, please ignore them (or better yet, [file an issue](https://github.com/beeradmoore/dlss-swapper/issues/new?template=other_issue.yml) and let us know)


## Sponsors

<table>
    <tr>
        <td style="width:50px">
            <img src="docs/images/sponsors/signpath.png" width="50" height="50" alt="SignPath">
        </td>
        <td>
            <strong>Of the original project.</strong> Free code signing on Windows provided by <a href="https://signpath.io/">SignPath.io</a>, certificate by <a href="https://www.signpath.com/solutions/for-open-source-community-foundation">SignPath Foundation</a>. Builds from this fork are <strong>not</strong> signed and carry no certificate.
        </td>
    </tr>
</table>