# Project working agreements

## Language and scope

- Communicate with Ashus in Czech and use informal address.
- Write repository documentation, code comments, CLI help, runtime messages, and
  test output in English. This project-specific agreement also applies to logs.
- Preserve existing comments and copyright/license notices. Use UTF-8; let Git
  handle line endings.
- Keep Windows support when reviewing Linux-oriented forks. Do not claim Linux
  compatibility based only on surviving POSIX code; compile and test it first.

## Versioning and releases

- Keep the version until Ashus explicitly requests a version change.
  Development work alone is not permission to increment it or publish a release.
- `VERSION` is the source of truth for executable and package versions. Use normal
  numeric fork versions, without a feature suffix such as `-https`.
- Build/release instructions should read the version from `VERSION`, rather than
  hardcoding a particular release in tag or archive commands.
- Distribute the contents of `dist` as a portable `.7z` archive with a SHA-256
  checksum. Keep the complete required runtime DLLs and license notices.
- Do not commit binaries, `dist`, toolchains, or release archives; do not use Git
  LFS for them. Do not push, publish, or create release tags without a request.
- A changelog is not required before the first release; the application is still under initial development.
