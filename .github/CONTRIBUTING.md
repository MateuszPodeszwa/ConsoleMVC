# Contributing to ConsoleMVC

Thank you for your interest in contributing to ConsoleMVC! This guide will help you get started.

## How to Contribute

### Reporting Bugs

- Use the [GitHub Issues](https://github.com/MateuszPodeszwa/ConsoleMVC/issues) tab to report bugs
- Include a clear description of the issue and steps to reproduce it
- Specify your .NET version and operating system

### Suggesting Features

- Open a [GitHub Issue](https://github.com/MateuszPodeszwa/ConsoleMVC/issues) with the `enhancement` label
- Describe the feature and why it would be useful

### Pull Requests

1. Fork the repository
2. Create a feature branch from `main`
3. Make your changes
4. Ensure your code builds without warnings and all tests pass, as CI checks on Linux, macOS and Windows:
   ```bash
   dotnet build ConsoleMVC.sln -c Release -warnaserror
   dotnet test ConsoleMVC.sln -c Release --no-build
   ```
5. Submit a pull request targeting `main`

#### PR Guidelines

- Keep changes focused and atomic, one feature or fix per PR
- Follow existing code style and conventions
- Write the tests first, and include XML documentation with an example for every public member
- Update the documentation in the same pull request when a change affects users: `readme.md`, the package readmes (`src/ConsoleMVC/PACKAGE_README.md`, `src/ConsoleMVC.Testing/PACKAGE_README.md`), the guides in `docs/guides/docs` (new guides go in `docs/guides/mkdocs.yml`), `CHANGELOG.md` and, when a roadmap item moves, `plannedfeatures.md`
- Larger features start with a design document in `docs/design` (UML and pseudocode)
- Use British English in code comments and documentation

## Development Setup

1. Clone your fork:
   ```bash
   git clone https://github.com/<your-username>/ConsoleMVC.git
   ```
2. Open `ConsoleMVC.sln` in your preferred IDE
3. Build the solution:
   ```bash
   dotnet build
   ```

## Project Structure

- `src/ConsoleMVC/`: Framework library (NuGet: ConsoleMVC.Framework), including the `Rendering/` engine and `Hosting/` (configuration and logging)
- `src/ConsoleMVC.Generators/`: Source generator that compiles `.cvw` views and C-View Markup (bundled in the framework package)
- `src/ConsoleMVC.Testing/`: Test host, scripted input and snapshots (NuGet: ConsoleMVC.Testing)
- `tests/`: `ConsoleMVC.Tests` (framework, rendering and the testing package) and `ConsoleMVC.Generators.Tests` (generator end-to-end)
- `docs/guides/`: MkDocs user guides, published with the API reference to GitHub Pages
- `templates/ConsoleMVC.App/`: Project template (NuGet template package)
- `docs/design/`: Design documents with UML diagrams and pseudocode

## Code of Conduct

Please be respectful and constructive in all interactions. We are committed to providing a welcoming and inclusive experience for everyone.

## Sponsorship

If you find ConsoleMVC useful, consider supporting the project:

- [GitHub Sponsors](https://github.com/sponsors/MateuszPodeszwa)
- [Patreon](https://www.patreon.com/c/mateuszpodeszwa)

## Questions?

Open a [Discussion](https://github.com/MateuszPodeszwa/ConsoleMVC/discussions) or reach out via Issues.

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](../LICENSE).