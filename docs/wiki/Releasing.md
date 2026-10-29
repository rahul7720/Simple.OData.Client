# Releasing

Releases are produced by the `Release` workflow (`.github/workflows/release.yml`). It builds and tests the solution in Release, packs `Nexus.Odata.Client`, creates the GitHub release with the `.nupkg` and `.snupkg` attached, and publishes the package to GitHub Packages.

## Cut a release

1. Set `<Version>` in `src/Simple.OData.Client/Simple.OData.Client.csproj`.
2. Add the notes: a new section at the top of `RELEASE_NOTES.md`, and optionally a full page at `docs/releases/v<version>.md` (used as the release body when present).
3. Run the **Release** workflow from the Actions tab on the branch to release. The workflow creates the `v<version>` tag at that commit and the release.

Pushing a `v<version>` tag triggers the same workflow; the tag must match the project version.

## Workflow inputs

| Input | Effect |
|---|---|
| `recreate_release` | If the release and tag already exist, delete them and recreate them at the current commit. |
| `publish_to_nuget` | Also push to nuget.org. Requires the `NUGET_API_KEY` repository secret and ownership of the package ID. |

## Where the package goes

- **GitHub Packages:** `https://nuget.pkg.github.com/rahul7720/index.json`, package `Nexus.Odata.Client`. Published on every release run with the workflow's own token.
- **Release assets:** the same `.nupkg` and `.snupkg`.
- **nuget.org:** only when `publish_to_nuget` is set.

## Other workflows

| Workflow | Trigger | Purpose |
|---|---|---|
| `PR Validation` | pull requests | build and test, uploads a TRX report |
| `CI Build & Package` | pushes to `main`/`master` | build, test and pack a CI-versioned package |
| `Wiki` | pushes that change `docs/wiki/**` | syncs this wiki from the repository |
