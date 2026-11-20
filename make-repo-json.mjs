// Builds repo.json (the plugin list Dalamud reads) from the built manifest.
// Usage: node make-repo-json.mjs   (after dotnet build -c Release)
import { readFileSync, writeFileSync } from "node:fs";

const m = JSON.parse(readFileSync("NightfallSync/bin/Release/NightfallSync/NightfallSync.json", "utf8").replace(/^﻿/, ""));
const zip = "https://github.com/ixnkz/NightfallSync/releases/latest/download/latest.zip";
const entry = {
  ...m,
  TestingAssemblyVersion: m.AssemblyVersion,
  IsHide: false,
  IsTestingExclusive: false,
  DownloadCount: 0,
  LastUpdate: Math.floor(Date.now() / 1000),
  DownloadLinkInstall: zip,
  DownloadLinkUpdate: zip,
  DownloadLinkTesting: zip,
};
writeFileSync("repo.json", JSON.stringify([entry], null, 2) + "\n");
console.log("repo.json", entry.InternalName, entry.AssemblyVersion);
