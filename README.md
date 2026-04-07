# Exif Editor

Author: Michał Szyma

## How to start

```
dotnet build
```

## Release

To create a new GitHub release with `.deb` (Linux) and `.exe` (Windows) installers, tag the commit using the `vX.Y.Z` pattern. The CI/CD pipeline will automatically build and publish the release assets.

### Steps

1. Create a tag:
   ```bash
   git tag v0.1.2
   ```

2. Push the tag to remote:
   ```bash
   git push origin v0.1.2
   ```

This will trigger the GitHub Actions workflow, which builds the `.deb` and `.exe` installers and attaches them to a new GitHub Release.