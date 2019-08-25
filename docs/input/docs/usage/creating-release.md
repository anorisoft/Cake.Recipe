---
Order: 10
Title: How to create a release
---

This document describes the suggested steps to release a new version using the `Cake.Recipe` script.

:::{.alert .alert-info}
Please note that the following steps assume you're using `Cake.Recipe` on GitHub and with a GitFlow workflow.
Both are not requirements though and you can adopt the steps to other environments.
:::

1. Create a release branch (eg. release/1.2.3).
2. Make sure that a GitHub milestone exists for this release.
3. Make sure there were issues for all changes with the appropriate labels and the correct milestone set.
4. Make sure that you have the following environment variables set in your local development environment:
   * [GITHUB_USERNAME](../fundamentals/environment-variables#github_username)
   * [GITHUB_PASSWORD](../fundamentals/environment-variables#github_password)
5. Create a GitHub release draft by running: `.\build.ps1 -target releasenotes` on Windows and on MacOS/Linux`./build.sh --target=releasenotes` (on unix build parameter `shouldRunGitVersion: true` needs to be set for version to be calculated by GitVersion).
6. Check the generated release notes and make required manual changes.
7. If release is ready finish release (merge back into `master` and `develop`) but don't tag the release yet.
8. Publish the draft release on GitHub.

The last step will tag the release and trigger another build including the publishing. The build will automatically publish the build artifacts to the GitHub release, publish to NuGet and notify about the new release through Twitter and Gitter, based on your specific settings.
