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
4. Ensure your code builds without warnings:
   ```bash
   dotnet build
   ```
5. Submit a pull request targeting `main`

#### PR Guidelines

- Keep changes focused and atomic — one feature or fix per PR
- Follow existing code style and conventions
- Include XML documentation for any public APIs
- Update the README if your change affects user-facing behavior

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

- `src/ConsoleMVC/` — Framework library (NuGet package)
- `templates/ConsoleMVC.App/` — Project template (NuGet template package)

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