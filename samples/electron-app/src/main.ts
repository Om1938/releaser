import { app, BrowserWindow } from "electron";
import { autoUpdater } from "electron-updater";
import { configureUpdates, fetchReleaseNotes, type ReleaserConfig } from "./releaser";

const releaser: ReleaserConfig = {
  baseUrl: process.env.RELEASER_URL ?? "https://releaser.example.com",
  appKey: "sample-app",
  // getContextToken: async () => (await fetch("https://api.example.com/me/update-context")).text(),
};

autoUpdater.on("update-available", (info) => console.log(`Update ${info.version} offered by Releaser`));
autoUpdater.on("update-not-available", () => console.log("No update for this installation"));
autoUpdater.on("error", (error) => console.error("Update check failed; staying on the current version", error));

app.whenReady().then(async () => {
  const window = new BrowserWindow({ width: 640, height: 400 });
  await window.loadURL(`data:text/html,<h1>Sample App ${app.getVersion()}</h1>`);

  await configureUpdates(releaser);
  await autoUpdater.checkForUpdatesAndNotify();
  const notes = await fetchReleaseNotes(releaser);
  console.log(`Release notes since ${app.getVersion()}:`, notes.map((n) => `${n.version}: ${n.title}`));
});

app.on("window-all-closed", () => app.quit());
