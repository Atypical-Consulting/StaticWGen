---
title: "Contribute"
description: "Contribute to StaticWGen, report issues, or get help from the community."
author: "Philippe Matray"
date: "2024-09-13"
keywords: "contact, contribute, open source, github, community"
image: "./assets/logo-wgen.webp"
---
# Get Involved

StaticWGen is open source and community-driven. Whether you want to report a bug, suggest a feature, or contribute code, there's a place for you.

## Links

- **GitHub**: [github.com/Atypical-Consulting/StaticWGen](https://github.com/Atypical-Consulting/StaticWGen)
- **Issues**: [Report a bug or request a feature](https://github.com/Atypical-Consulting/StaticWGen/issues)
- **Discussions**: [Ask questions and share ideas](https://github.com/Atypical-Consulting/StaticWGen/issues)

## Contributing

We welcome contributions of all kinds:

1. **Bug reports** --- found something broken? Open an issue with steps to reproduce
2. **Feature requests** --- have an idea? Start a discussion first
3. **Pull requests** --- fork, branch, code, test, PR
4. **Documentation** --- typo fixes, better explanations, new guides
5. **Content examples** --- share your Markdown templates and themes

### Development Setup

```bash
# Clone the repo
git clone https://github.com/Atypical-Consulting/StaticWGen.git
cd StaticWGen

# Build and generate the site
nuke

# The generated site is in /output
```

### Project Structure

```
StaticWGen/
  build/          # NUKE build interfaces (C#)
  src/            # Core library (Markdown processing)
  input/          # Content files (Markdown + assets)
  template/       # Scriban HTML template
  output/         # Generated static site
```

## License

StaticWGen is released under the MIT License. Use it for personal projects, commercial sites, or anything in between.

---

### Built by Philippe Matray

A .NET developer passionate about build automation, clean architecture, and developer experience.
