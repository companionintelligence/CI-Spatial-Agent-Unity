# XREAL SDK Required

This project requires the XREAL SDK to build. The SDK is not included in this repository due to licensing restrictions.

## Quick Setup

1. Download `com.xreal.xr.tar.gz` from https://developer.xreal.com/download
2. In Unity: Window → Package Manager → + → Add package from tarball
3. Select the downloaded `com.xreal.xr.tar.gz` file

For detailed setup instructions, see [SETUP.md](../SETUP.md) in the root of this repository.

## Why is the SDK not included?

The XREAL SDK is proprietary software that requires:
- Accepting XREAL's terms of service
- Creating a developer account
- Manual download from their developer portal

Therefore, it cannot be included directly in this Git repository.

## Alternative: Local Package Installation

If you prefer to keep the SDK in your local repository (gitignored), you can:

1. Create a `Packages/com.xreal.xr/` folder
2. Extract the SDK tarball contents there
3. Unity will automatically detect it as a local package

Note: This folder is already in `.gitignore` to prevent accidentally committing proprietary code.
