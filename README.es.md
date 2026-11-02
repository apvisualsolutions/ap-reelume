# AP Reelume

Reproductor y biblioteca de vídeo local para Windows 11.

- **Descargar:** [última versión](https://github.com/apvisualsolutions/ap-reelume/releases/latest).
- **Licencia:** propia, en [`LICENSE`](LICENSE). Avisos de terceros en
  [`docs/release/THIRD-PARTY-NOTICES.es.md`](docs/release/THIRD-PARTY-NOTICES.es.md).
- **Componentes LGPL:** su fuente correspondiente se publica junto a cada versión en *Releases*.
- **Seguridad:** ver [`SECURITY.md`](SECURITY.md).

## Comprobar una descarga

Cada versión publica `SHA256SUMS.txt`, con el hash de cada archivo, y su firma
`SHA256SUMS.txt.minisig`. La firma se comprueba con [minisign](https://jedisct1.github.io/minisign/)
y esta clave pública, la misma que lleva dentro la aplicación para aceptar una actualización:

```text
RWTnzH0X9Phns/IIMorPQwevjzJTZz4hKIHLKshZY/vfi2iHm2gDvM+c
```

```powershell
minisign -Vm SHA256SUMS.txt -P RWTnzH0X9Phns/IIMorPQwevjzJTZz4hKIHLKshZY/vfi2iHm2gDvM+c
Get-FileHash .\<archivo-descargado> -Algorithm SHA256
```

El hash tiene que coincidir con su línea de `SHA256SUMS.txt`. La clave solo sirve para comprobar
firmas; con ella no se puede firmar nada.

[English](README.en.md)
