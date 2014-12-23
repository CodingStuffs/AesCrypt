AesCrypt
========
Usage:
 To encrypt: AesCrypt.exe e <file path>
 To decrypt: AesCrypt.exe d <file path> <iv> <key>
 
Encryption generates an IV and key, both of which are needed to recover the original file. Keep these in a safe place!


Examples:
 Encrypting a file:
 ```
 c:\Files>AesCrypt.exe e "important document.pdf" > X:\secret.key
 ```
 This will generate a file with a long (~100 character) name in C:\Files and the key information in X:\secret.key, which can be a thumb drive for example.
 Contents of secret.key look like this:
 ```
 LC6HWHXFHCUXVM2JRJM7JFSGZTYJ4NIO7BB4UAH2DV3JXFSJIMZ33HGDBEAG4SDLB2NTG4EW7IAPPNWJRATNS7GZ6R2LQJDPKRDQ6IY Y2V5eVqp7b4BbBnduC9xhA== zMnpVwbE6SWvDXSyLEg0zTz2PO4aAmjPArvpPiDlcNM=
 ```

 Decrypting a file:
 ```
 c:\Files>AesCrypt.exe d LC6HWHXFHCUXVM2JRJM7JFSGZTYJ4NIO7BB4UAH2DV3JXFSJIMZ33HGDBEAG4SDLB2NTG4EW7IAPPNWJRATNS7GZ6R2LQJDPKRDQ6IY Y2V5eVqp7b4BbBnduC9xhA== zMnpVwbE6SWvDXSyLEg0zTz2PO4aAmjPArvpPiDlcNM=
 Decrypting 'c:\Files\important document.pdf', 11,767,391 bytes
 Decrypted 'important document.pdf'
 ```
 You can pretty much just paste the contents of X:\secret.key into AesCrypt command line.

 Details:
 Encryption produces a file with its own SHA512 hash as the name, encoded in Base32. You can rename it to anything you want, decryption will still work with whatever filename you choose + IV + key from encryption output. For example, if you rename LC6HWHXFHCUXVM2JRJM7JFSGZTYJ4NIO7BB4UAH2DV3JXFSJIMZ33HGDBEAG4SDLB2NTG4EW7IAPPNWJRATNS7GZ6R2LQJDPKRDQ6IY to "random data.bin", decryption command will look like *AesCrypt.exe d "random data.bin" Y2V5eVqp7b4BbBnduC9xhA== zMnpVwbE6SWvDXSyLEg0zTz2PO4aAmjPArvpPiDlcNM=* and still produce "important document.pdf". Note that the original full path of the file will be shown (C:\Files\important document.pdf), this is just for reference, decrypted file is created in whatever directory you are running AesCrypt in.