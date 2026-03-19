# Contributing to StaticWGen

Thank you for your interest in contributing to StaticWGen! This guide will help you get started.

## Getting Started

1. Fork the repository
2. Clone your fork: `git clone https://github.com/your-username/StaticWGen.git`
3. Create a feature branch from `dev`: `git checkout -b feature/my-feature dev`
4. Install prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Development Workflow

### Building

```bash
./build.sh BuildWebsite --site-base-url "http://localhost:3000" --site-title "Test"
```

### Development Server

```bash
./build.sh Watch --site-base-url "http://localhost:3000" --site-title "Test"
```

This starts a live-reload server on http://localhost:3000.

### Running Validation

```bash
./build.sh Validate --site-base-url "http://localhost:3000" --site-title "Test"
```

## Submitting Changes

1. Ensure the build passes locally
2. Write clear, concise commit messages
3. Push your branch and open a pull request against `dev`
4. Fill out the PR template

## Branch Strategy

- `main` — stable releases
- `dev` — active development (target your PRs here)

## Code Style

- Follow existing C# conventions in the codebase
- Keep build targets as focused interfaces (one concern per file)
- Use Serilog structured logging (`Information`, `Warning`, `Error`)

## Reporting Issues

Use the [issue templates](https://github.com/Atypical-Consulting/StaticWGen/issues/new/choose) for bug reports and feature requests.

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
