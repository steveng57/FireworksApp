# FireworksApp MSIX install guide

This guide covers the one-time certificate installation and installing the FireworksApp MSIX package on Windows 10/11.

## Files in the release
- `FireworksApp-<version>-win-x64.msix`: the application package
- `FireworksApp.cer`: the public signing certificate (no private key)
- `FireworksApp-Install-Instructions.md`: this guide

## One-time certificate installation (required for the MSIX to be trusted)
1. Sign in with an account that can install certificates for **Local Machine** (admin).
2. Locate `FireworksApp.cer` from the release download.
3. Double-click the `.cer` file to open Certificate Import Wizard.
4. When asked for the store location, choose **Local Machine** (not Current User).
5. When prompted for a store, pick **Place all certificates in the following store** and select **Trusted Root Certification Authorities**. Complete the wizard.
6. Close the wizard. You can verify in `certlm.msc` under **Trusted Root Certification Authorities → Certificates** and **Trusted People → Certificates** that `FireworksApp` is present.

## Install the MSIX package
1. Ensure the certificate is trusted per the steps above.
2. Double-click `FireworksApp-<version>-win-x64.msix`.
3. In the installer dialog, confirm the **Publisher** matches the certificate subject (e.g., `CN=FireworksApp` or your configured subject) and proceed.
4. When complete, launch FireworksApp from Start Menu or the installer dialog.

## Troubleshooting
- **Error 0x800B0109**: The signing chain is not trusted. Re-import `FireworksApp.cer` into **Local Machine → Trusted Root Certification Authorities** and **Trusted People**, then retry.
- **Blocked by policy**: Enable **Sideload apps** or have an admin adjust the policy.
- **Publisher mismatch**: The package `Publisher` must match the certificate subject used to sign it. Download the matching `.msix` and `.cer` from the same release.

Once a publicly trusted code-signing certificate is installed, these one-time steps will no longer be necessary, and you can freely update or reinstall the MSIX as desired.
