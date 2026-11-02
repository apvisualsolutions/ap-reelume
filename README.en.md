# AP Reelume

Local video player and library for Windows 11.

- **Download:** [latest release](https://github.com/apvisualsolutions/ap-reelume/releases/latest).
- **License:** proprietary, in [`LICENSE`](LICENSE). Third-party notices in
  [`docs/release/THIRD-PARTY-NOTICES.en.md`](docs/release/THIRD-PARTY-NOTICES.en.md).
- **LGPL components:** their corresponding source is published with each version under *Releases*.
- **Security:** see [`SECURITY.md`](SECURITY.md).

## Checking a download

Each version publishes `SHA256SUMS.txt`, with the hash of every file, and its signature
`SHA256SUMS.txt.minisig`. The signature is checked with [minisign](https://jedisct1.github.io/minisign/)
and this public key, the same one the application carries inside to accept an update:

```text
RWTnzH0X9Phns/IIMorPQwevjzJTZz4hKIHLKshZY/vfi2iHm2gDvM+c
```

```powershell
minisign -Vm SHA256SUMS.txt -P RWTnzH0X9Phns/IIMorPQwevjzJTZz4hKIHLKshZY/vfi2iHm2gDvM+c
Get-FileHash .\<downloaded-file> -Algorithm SHA256
```

The hash has to match its line in `SHA256SUMS.txt`. The key can only check signatures; nothing can
be signed with it.

[Español](README.es.md)
