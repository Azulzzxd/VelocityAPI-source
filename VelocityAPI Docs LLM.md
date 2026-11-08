# Velocity public-API Documentation (v31.03)

A .NET 10 class library for attaching to Velocity-enabled processes, sending Lua scripts to them over a named pipe, and validating a Velocity license key.

Written from the VelocityAPI source code (net10.0 project) and checked against the compiled DLL.

## Quick facts

- Assembly: `VelocityAPI` (version 1.0)
- Target framework: `net10.0` (for WPF/WinForms use `net10.0-windows`). Older .NET versions will not load it unless retargeted and rebuilt.
- Platform: Windows only (`bcrypt.dll`, registry, SMBIOS firmware tables, Windows named pipes)
- NuGet dependency: `BouncyCastle` 1.8.9 (provides `BouncyCastle.Crypto.dll`, used for X25519)
- Namespaces: `VelocityAPI` (`VelAPI`, `VelocityStates`, `DownloadUrlData`), `Velocity` (`VelocityLicense`), global (`AESEncryption`)
- License key location: `HKEY_CURRENT_USER\SOFTWARE\Velocity\LicenseKey` (REG_SZ)
- **Requires administrator rights.** The injector launched by `Attach` needs elevation.

---

## Requirements and setup

### Required dependencies

| Dependency | Details |
|---|---|
| Windows 10/11 x64 | Windows-only APIs. Not Linux/macOS. |
| .NET 10 SDK (runtime for end users) | Project targets `net10.0`. |
| Visual Studio | Version that supports .NET 10 and the `.slnx` solution format (source authored in Visual Studio 2026). Install the ".NET desktop development" workload. |
| BouncyCastle NuGet package | Version `1.8.9`, as referenced by `VelocityAPI.csproj`. `BouncyCastle.Crypto.dll` must sit next to your app. |
| Administrator rights | Needed by the injector. Restart Visual Studio as administrator to debug. |
| Internet access | `realvelocity.xyz` (auto-update in `StartCommunication`) and `getvelocity.llc` (license auth). |

Built into .NET (no extra install): `System.Net.Http`, `System.Text.Json`, `System.IO.Pipes`, `System.Security.Cryptography`, `Microsoft.Win32.Registry`.

The source repository does not distribute compiled DLLs. Build `VelocityAPI` yourself, or use a DLL you already have.

### 1. Build the DLL

1. Open `VelocityAPI.slnx` in Visual Studio.
2. Set configuration to Release.
3. Build > Build Solution (Ctrl+Shift+B).
4. Output: `VelocityAPI\bin\Release\net10.0\` containing `VelocityAPI.dll` and `BouncyCastle.Crypto.dll`.

### 2. Add it to your project (Visual Studio UI)

**Option A: reference the DLL**

1. Solution Explorer > right-click your project's Dependencies > Add Project Reference...
2. Browse tab > Browse... > select `VelocityAPI.dll` > Add > OK.
3. Expand Dependencies > Assemblies, select VelocityAPI, and in Properties set Copy Local = Yes.

**Option B: reference the project**

1. Right-click the solution > Add > Existing Project... > `VelocityAPI\VelocityAPI.csproj`.
2. Right-click your project's Dependencies > Add Project Reference... > Projects > tick VelocityAPI > OK.

Option B brings BouncyCastle in automatically. With Option A, add it yourself: right-click project > Manage NuGet Packages... > Browse > search `BouncyCastle` > version 1.8.9 > Install.

Equivalent project file entry:

```xml
<ItemGroup>
  <Reference Include="VelocityAPI">
    <HintPath>libs\VelocityAPI.dll</HintPath>
    <Private>True</Private>
  </Reference>
  <PackageReference Include="BouncyCastle" Version="1.8.9" />
</ItemGroup>
```

Do not set the DLL's Build Action to Embedded Resource. .NET resolves referenced assemblies from the output folder, so the DLL and `BouncyCastle.Crypto.dll` must be copied next to your `.exe` (Copy Local = Yes, or Copy to Output Directory = Copy always if added as an item).

### 3. Require administrator

1. Right-click project > Add > New Item... > Application Manifest File (Windows), keep name `app.manifest`.
2. Set the execution level:

```xml
<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
```

Windows will show a UAC prompt on every start. Restart Visual Studio as administrator to build/debug. Verify under Project > Properties > Application > Win32 resources > Manifest.

### 4. Hook up start and stop (WPF)

In the designer select the window, open Properties > Events (lightning icon), double-click `Loaded` and `Closing`. Fill them in as in the Quickstart below.

### 5. Files created at runtime

Everything is relative to the app's current working directory (normally the folder with your `.exe`):

```
YourApp.exe
VelocityAPI.dll
BouncyCastle.Crypto.dll
Bin\                     created by StartCommunication
  Decompiler.exe         downloaded by auto-update, started by StartCommunication
  erto3e4rortoergn.exe   downloaded injector, launched by Attach
  current_version.txt    last installed version
AutoExec\                created
Scripts\                 created
Workspace\               created; its path is sent to attached clients
```

---

## Quickstart (WPF)

```csharp
using VelocityAPI;

public partial class MainWindow : Window
{
    private readonly VelAPI velocityApi = new VelAPI();

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Blocking network + file work (auto-update): keep it off the UI thread.
        // Throws if the update server is unreachable; wrap in try/catch in production.
        await Task.Run(() => velocityApi.StartCommunication());
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        velocityApi.StopCommunication();
    }

    private async Task<bool> AttachAsync(int pid)
    {
        VelocityStates state = await velocityApi.Attach(pid, 15000);
        return state == VelocityStates.Attached;
    }

    private void RunScript(string lua, int pid)
    {
        VelocityStates result = velocityApi.Execute(lua, new[] { pid });
        if (result == VelocityStates.NotAttached)
            MessageBox.Show("Attach first.");
    }
}
```

Results: `Attach` returns `Attached` or `Error` (timeout). `Execute` returns `Executed` or `NotAttached`.

---

## API reference

### class `VelocityAPI.VelAPI`

Main entry point. Tracks attached processes and sends Lua scripts to them.

#### Constructor

```csharp
public VelAPI()
```

Creates an instance. Nothing starts until `StartCommunication()`.

#### Public fields

```csharp
public VelocityStates VelocityStatus = VelocityStates.NotAttached;
public List<int> injected_pids;
```

- `VelocityStatus`: last state set by `Attach` (`Attaching` while waiting, then `Attached` or `Error`).
- `injected_pids`: PIDs currently tracked as attached. The library mutates it under an internal private lock, so avoid iterating it from your own threads; use `IsAttached` and `AttachedCount`.

#### Property

```csharp
public int AttachedCount { get; }
```

Number of clients whose Lua pipe currently exists (dead entries pruned first).

#### `void StartCommunication()`

Prepares the working folder, updates helper binaries, starts the background service.

1. Creates `Bin`, `AutoExec`, `Workspace`, `Scripts` if missing.
2. Auto-update: reads `download_links.json` and `current_version.txt` from `realvelocity.xyz`. If the version differs from `Bin\current_version.txt`, deletes and re-downloads `Bin\erto3e4rortoergn.exe` (injector) and `Bin\Decompiler.exe`.
3. Calls `StopCommunication()`, then starts `Bin\Decompiler.exe` hidden with redirected input/output/error.
4. Starts a 100 ms timer that prunes detached clients and sends each attached client `setworkspacefolder: <cwd>\Workspace` (Base64).

Synchronous; blocks on network I/O; throws if the update server is unreachable.

#### `void StopCommunication()`

Stops the timer, kills and disposes the `Decompiler.exe` process, clears `injected_pids`. Safe to call repeatedly. Call on app close.

#### `Task<VelocityStates> Attach(int pid, int timeoutMs = 15000)`

Attaches to process `pid`.

- If the pipe for `pid` already exists: returns `Attached` immediately.
- Otherwise sets `VelocityStatus = Attaching` and launches `Bin\erto3e4rortoergn.exe <pid>` (visible console window; does not wait for exit).
- Polls every 100 ms for the pipe. When it appears the PID is tracked and `Attached` is returned.
- If `timeoutMs` passes: returns `Error`.

Returns `Attached` or `Error`. `Bin\erto3e4rortoergn.exe` must exist, so call `StartCommunication` first.

#### `bool IsAttached(int pid)`

Checks whether the Lua pipe for `pid` exists. If yes: adds the PID to the tracked list (if absent) and returns `true`. If no: removes it and returns `false`. Detection is purely pipe-based, so clients attached by other means are also detected.

#### `VelocityStates Execute(string script, IEnumerable<int> targetPids)`

Sends a Lua script to every listed PID that is attached.

- `script`: Lua source; Base64-encoded (UTF-8) before sending.
- `targetPids`: processes to run in; PIDs failing `IsAttached` are skipped.

Returns `NotAttached` if none are attached, otherwise `Executed`. "Executed" means "sent": the script is written to each pipe on a background thread (fire and forget). Script errors and output are not returned.

#### `static bool SaveLicenseKeyToRegistry(string key)`

Creates `HKEY_CURRENT_USER\SOFTWARE\Velocity` if needed and writes the trimmed key as two REG_SZ values: `LicenseKey` and `RBX_EXT_LicenseKey`. Returns `false` for null/empty/whitespace keys or on any registry error, otherwise `true`.

#### `static string Base64Encode(string plainText)`

UTF-8 bytes of `plainText`, Base64-encoded.

#### `static byte[] Base64Decode(string plainText)`

Decodes Base64 text to bytes. Throws `FormatException` on invalid input.

### enum `VelocityAPI.VelocityStates`

```csharp
public enum VelocityStates
{
    Attaching,      // 0
    Attached,       // 1
    NotAttached,    // 2
    NoProcessFound, // 3
    TamperDetected, // 4
    Error,          // 5
    Executed        // 6
}
```

| Value | Used by | Meaning |
|---|---|---|
| `Attaching` | `VelocityStatus` | Attach is waiting for the pipe. |
| `Attached` | `Attach` | Client's pipe exists; ready for scripts. |
| `NotAttached` | `Execute`, default `VelocityStatus` | No listed PID is attached. |
| `NoProcessFound` | not used | Defined but never returned in this version. |
| `TamperDetected` | not used | Defined but never returned in this version. |
| `Error` | `Attach` | Pipe did not appear within `timeoutMs`. |
| `Executed` | `Execute` | Script was sent to at least one attached process. |

Handle `NoProcessFound` and `TamperDetected` defensively in a `default` branch.

### Licensing: class `Velocity.VelocityLicense : IDisposable`

#### Where the key is stored

```
Computer\HKEY_CURRENT_USER\SOFTWARE\Velocity\LicenseKey   [REG_SZ]
```

`VelAPI.SaveLicenseKeyToRegistry` also writes the same value to `RBX_EXT_LicenseKey`.

```csharp
using Microsoft.Win32;

static string? ReadLicenseKey()
{
    using RegistryKey? rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Velocity");
    return rk?.GetValue("LicenseKey") as string;   // null if not set
}
```

#### Accepted key formats

`AuthenticateAsync` rejects anything else locally with `"bad key format"`.

| Kind | Format |
|---|---|
| Paid personal | `V****-****-****-****`: 20 chars, uppercase hex, e.g. `VABCD-1234-EF56-7890` |
| Work.ink free key | 64 lowercase hex characters |
| Admin shared key | 3 to 32 chars from `A-Z a-z 0-9 _ -` |

#### Members

```csharp
public VelocityLicense()
public bool IsAuthenticated { get; }
public Task<AuthResult> AuthenticateAsync(string key)
public Task<LicenseStatus?> QueryStatusAsync()
public void Dispose()
```

- Constructor: creates the client for `https://getvelocity.llc`.
- `IsAuthenticated`: true after a successful `AuthenticateAsync`.
- `AuthenticateAsync(key)`: validates format; computes the hardware ID (fails with `"hwid failed"` if empty); generates an ephemeral X25519 key pair, derives a transit key from the server public key (HKDF), and sends the encrypted request to `POST /api/v1/license/auth` with key, hardware IDs and product `"roblox"`. On `TIMESTAMP_SKEW` it adjusts for the server clock and retries once. On failure `Error` holds the reason, e.g. `bad key format`, `hwid failed`, `server: ...`, `no valid response after retry`, `response decrypt failed`.
- `QueryStatusAsync()`: `POST /api/v1/license/status`, encrypted with the session key. Returns `null` if not authenticated, or on network/5xx/decrypt failure. On HTTP 4xx only `Code` is set. Otherwise a populated `LicenseStatus`.
- `Dispose()`: clears authenticated state, zeroes the session key, disposes the `HttpClient`. Use `using var`.

#### `VelocityLicense.AuthResult`

```csharp
public sealed record AuthResult(bool Ok, string Error = "", string Product = "", string ExpiresAt = "");
```

| Property | Type | Description |
|---|---|---|
| `Ok` | bool | Authentication succeeded. |
| `Error` | string | Failure reason; empty on success. |
| `Product` | string | Product the key belongs to. |
| `ExpiresAt` | string | Expiry timestamp from the server. |

#### `VelocityLicense.LicenseStatus`

```csharp
public sealed record LicenseStatus { /* all init-only */ }
```

| Property | Type | Description |
|---|---|---|
| `Valid` | bool | Key is currently valid. |
| `Expired` | bool | Key has expired. |
| `SecondsRemaining` | long | Seconds left. `-1` means lifetime (also the default). |
| `Source` | string | Where the key came from. |
| `Product` | string | Licensed product. |
| `Duration` | string | `"day"`, `"week"`, `"monthly"` or `"lifetime"`. |
| `ExpiresAt` | string | ISO-8601; empty for lifetime keys. |
| `Code` | string | Set on failure (e.g. `http-4xx` or the server's code). |

#### Example

```csharp
using Velocity;
using VelocityAPI;

string? key = ReadLicenseKey();                       // HKCU\SOFTWARE\Velocity\LicenseKey
if (key is null) { Console.WriteLine("No key saved."); return; }

using var license = new VelocityLicense();

VelocityLicense.AuthResult auth = await license.AuthenticateAsync(key);
if (!auth.Ok) { Console.WriteLine($"Auth failed: {auth.Error}"); return; }

VelocityLicense.LicenseStatus? status = await license.QueryStatusAsync();
if (status is null) { Console.WriteLine("Status query failed."); return; }

if (status.SecondsRemaining < 0)
    Console.WriteLine("Lifetime key");
else
    Console.WriteLine($"Expires in {status.SecondsRemaining}s ({status.Duration})");

VelAPI.SaveLicenseKeyToRegistry(key);                 // persist for next launch
```

### Helper types

#### `AESEncryption` (global namespace)

Password-based AES-GCM via Windows CNG (`bcrypt.dll`). Key derived with PBKDF2-SHA256 and a random salt.

```csharp
public static string Encrypt(string plaintext, string password)
public static string Decrypt(string ciphertext, string password)
```

- `Encrypt`: returns Base64 of `salt(16) || nonce(12) || ciphertext || tag`. Throws `ArgumentException` if plaintext or password is empty. Output differs on every call (random salt and nonce).
- `Decrypt`: reverses `Encrypt`. Throws `ArgumentException` for empty input, an error for data that is too short, and a cryptographic error ("Wrong password or corrupted data") on bad password or tampered data.

```csharp
string sealedText = AESEncryption.Encrypt("secret", "hunter2");
string plain      = AESEncryption.Decrypt(sealedText, "hunter2");
```

Used internally to decrypt download links during auto-update.

#### `VelocityAPI.DownloadUrlData`

Data holder for `download_links.json`.

| Property | Type | Meaning |
|---|---|---|
| `L1` | string? | Encrypted URL of `Decompiler.exe`. |
| `L2` | string? | Encrypted URL of the injector. |
| `question` | string? | Password used with `AESEncryption.Decrypt`. |

---

## Internals and data use

### Named pipes

Each attached client exposes the pipe `\\.\pipe\uoQcySKXSUxxJNpVQyatpHQwYoGfhcbh_<pid>`. Existence is tested with `WaitNamedPipe`; this is the only attach detection. Scripts are written to the pipe as Base64 text from a short-lived background thread. Errors are swallowed.

### Network access

| Host | When | What |
|---|---|---|
| `realvelocity.xyz` | `StartCommunication` | `assets/current_version.txt` and `assets/download_links.json`, then the two executables if the version changed. |
| `getvelocity.llc` | `AuthenticateAsync`, `QueryStatusAsync` | Encrypted license requests (`/api/v1/license/auth`, `/api/v1/license/status`). |

### Hardware ID (data sent to the license server)

32-byte HKDF hash (hex) of: Windows `MachineGuid`, CPU vendor string and feature flags, system volume serial, SMBIOS system UUID. A legacy ID (same, but with the network adapter MAC address instead of the SMBIOS UUID) is also sent when it differs. Mention this in your privacy notice if you distribute software that calls `AuthenticateAsync`.

### Registry

| Key | Value | Type | Access |
|---|---|---|---|
| `HKCU\SOFTWARE\Velocity` | `LicenseKey` | REG_SZ | written by `SaveLicenseKeyToRegistry` |
| `HKCU\SOFTWARE\Velocity` | `RBX_EXT_LicenseKey` | REG_SZ | written by `SaveLicenseKeyToRegistry` |
| `HKLM\SOFTWARE\Microsoft\Cryptography` | `MachineGuid` | REG_SZ | read for the hardware ID |

### Auto-update

`StartCommunication` may replace `Bin\Decompiler.exe` and `Bin\erto3e4rortoergn.exe` with files fetched from the update host, then launches them (the injector on `Attach`, with administrator rights). Only use it where that is acceptable, and keep the app's working directory writable only by trusted users.

### License wire format

Requests are JSON envelopes `{"v":1, ...}` carrying an AES-256-GCM payload (`iv(12) || tag(16) || ciphertext`, hex-encoded). The additional authenticated data binds the timestamp and nonce; replies use a `|resp` suffix. A server-reported clock skew is corrected once per call.
