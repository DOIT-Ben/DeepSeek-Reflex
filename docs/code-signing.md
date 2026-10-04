# Code signing policy

## Current status

As of 2026-10-04, DeepSeek-Reflex releases are **unsigned**. A SHA-256 checksum checks file integrity against the published checksum; it is not a trusted publisher signature. Application preparation does not mean a signing subscription or certificate has been approved.

The project intends to apply for the [SignPath Foundation open-source program](https://signpath.org/apply.html). Approval is at the provider's discretion, including project reputation, dependencies and verifiable build origin. The bundled WebView2 SDK libraries and separately installed Microsoft runtime must be disclosed for eligibility review. No signing credentials, account permissions or provider subscription are configured in this repository yet.

## Responsibilities

- Maintainer, contributor reviewer and intended release-signing approver: [DOIT-Ben](https://github.com/DOIT-Ben).
- Before enabling signing, the maintainer must confirm MFA on GitHub and the signing service, complete provider review, and approve the exact release artifact.
- Community pull requests require maintainer review before they become release inputs.

## Privacy

Reflex loads the official DeepSeek website in a dedicated WebView2 profile. Conversation submission is performed by the user on that website; the project does not run its own conversation server or analytics endpoint. Explicit selected-text import may read the selected text or use clipboard fallback, and the user reviews and sends the draft. Website and Microsoft runtime behavior follows the providers' own policies.

See [README: Data and privacy](../README.md#数据与隐私), [DeepSeek privacy policy](https://cdn.deepseek.com/policies/zh-CN/deepseek-privacy-policy.html) and [Microsoft privacy statement](https://privacy.microsoft.com/privacystatement). The signing provider receives approved build artifacts and maintainer application information; it must never receive user profile files, cookies, chat histories or credentials.

## Planned signing pipeline

1. Build from the reviewed release commit in the public GitHub Actions workflow.
2. Sign project-owned executable artifacts through the approved provider, with a trusted timestamp. Do not re-sign Microsoft dependencies under the project's identity.
3. Build the installer from the signed application; configure signing of the generated uninstaller and the final installer through the provider's supported process.
4. Verify each required Authenticode signature and publisher identity, then package the ZIP and generate hashes of the final signed bytes.
5. Publish only artifacts from that approved run. A failed or unavailable signing operation must block a release advertised as signed; never silently substitute unsigned files.

After approval and the first verified signed release, update this page with the actual service, certificate publisher, signing workflow and required attribution. Do not claim provider sponsorship before it is granted. Under the Foundation program, the certificate publisher is SignPath Foundation rather than the maintainer's personal company name.

## Alternatives

[Microsoft's code-signing options](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options) describe paid CA certificates and Microsoft Store MSIX distribution. Self-signed development certificates are not a substitute for public-trust signing. Signing alone does not guarantee immediate SmartScreen reputation.
