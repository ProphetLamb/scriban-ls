export function getLanguageServerBinaryFilePath(): Promise<string> {
  return Promise.resolve(
    `${__dirname}/../../ScribanLanguage.Server/bin/Debug/net10.0/ScribanLanguage.Server.exe`
  );
}