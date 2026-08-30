import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const nativeRoot = path.join(
  root,
  "platforms",
  "windows",
  "SparkDashDesktopTile"
);
const tileRoot = path.join(nativeRoot, "SparkDash.DesktopTile");

function read(relativePath) {
  return fs.readFileSync(path.join(tileRoot, relativePath), "utf8");
}

const project = read("SparkDash.DesktopTile.csproj");
assert.match(project, /<UseWPF>true<\/UseWPF>/);
assert.match(project, /<UseWindowsForms>true<\/UseWindowsForms>/);
assert.match(project, /<OutputType>WinExe<\/OutputType>/);
assert.match(project, /SparkDash\.StatusCore\.csproj/);
assert.doesNotMatch(project, /WidgetCore|WidgetProvider/);
assert.match(project, /SparkDash\.DesktopTile\.Core\.csproj/);

const window = read("MainWindow.xaml");
assert.match(window, /WindowStyle="None"/);
assert.match(window, /AllowsTransparency="True"/);
assert.match(window, /ShowInTaskbar="False"/);
assert.match(window, /ResizeGrip/);
assert.match(window, /<Grid Grid.Row="3" Margin="0,0,20,0">/);
assert.match(window, /Open sparkDash/);
assert.match(window, /HorizontalContentAlignment="Stretch"/);
assert.match(window, /Width="{Binding ActualWidth, RelativeSource={RelativeSource AncestorType={x:Type ItemsControl}}}"/);
assert.match(window, /<Polyline/);
assert.match(window, /Points="{Binding GenerationPoints}"/);
assert.match(window, /ColumnDefinition Width="1\.7\*" MinWidth="120"/);
assert.match(window, /Text="GEN TOK\/S"/);
assert.match(window, /Text="{Binding ModelText}"/);
assert.match(window, /DataTrigger Binding="{Binding ThermalThrottle}" Value="True"/);
assert.match(window, /BorderBrush" Value="#EF4444"/);
assert.match(window, /Grid\.Column="1"[\s\S]*Grid\.RowSpan="3"/);

const viewModel = read("TileViewModel.cs");
assert.match(viewModel, /public ObservableCollection<TileUnitViewModel> Units/);
assert.match(viewModel, /GenerationSparkline/);
assert.match(viewModel, /GenerationPoints/);
assert.match(viewModel, /ModelText/);
assert.match(viewModel, /ThermalThrottle/);
const startupRegistration = read("StartupRegistration.cs");
assert.match(startupRegistration, /StringComparison\.OrdinalIgnoreCase/);
assert.match(startupRegistration, /Environment\.ProcessPath/);

const appCode = read("App.xaml.cs");
assert.match(appCode, /EventWaitHandle/);
assert.match(appCode, /RegisterWaitForSingleObject/);
assert.match(appCode, /ActivationEventName/);
assert.match(appCode, /ShowFromExternalActivation/);
assert.match(appCode, /OnSessionEnding/);

const codeBehind = read("MainWindow.xaml.cs");
assert.match(codeBehind, /PrepareForSystemShutdown/);
assert.match(codeBehind, /StatusSummaryClient/);
assert.match(codeBehind, /RefreshInterval = TimeSpan\.FromSeconds\(1\)/);
assert.match(codeBehind, /TileSummaryParser/);
assert.match(codeBehind, /DispatcherTimer/);
assert.match(codeBehind, /NotifyIcon/);
assert.match(codeBehind, /DragMove/);
assert.match(codeBehind, /TileSettingsStore/);
assert.match(codeBehind, /StartupRegistration/);
assert.match(codeBehind, /http:\/\/127\.0\.0\.1:5555/);
assert.doesNotMatch(codeBehind, /\/api\/(?:shutdown|wake)|update-hermes/i);

const statusClient = fs.readFileSync(
  path.join(nativeRoot, "SparkDash.StatusCore", "StatusSummaryClient.cs"),
  "utf8"
);
assert.match(statusClient, /api\/status\/summary/);
assert.doesNotMatch(statusClient, /widget/i);

const publisher = fs.readFileSync(
  path.join(nativeRoot, "tools", "publish_desktop_tile.ps1"),
  "utf8"
);
assert.match(publisher, /dotnet publish/);
assert.match(publisher, /stagingRoot/);
assert.match(publisher, /packages\.lock\.json/);
assert.match(publisher, /RestorePackagesWithLockFile=false/);
assert.match(publisher, /RUNNER_TEMP/);
assert.match(publisher, /Refusing to delete/);

const installer = fs.readFileSync(
  path.join(nativeRoot, "tools", "install_desktop_tile.ps1"),
  "utf8"
);
assert.match(installer, /publish_desktop_tile\.ps1/);
assert.match(installer, /SparkDash\.DesktopTile\.exe/);
assert.match(installer, /LOCALAPPDATA/);
assert.match(installer, /Wait-Process/);
assert.match(installer, /Start-Sleep -Milliseconds/);
assert.match(installer, /Start-Process \$installedExecutable -WorkingDirectory \$installDirectory/);
assert.doesNotMatch(installer, /PublishSingleFile|RestoreLockedMode/);

const uninstaller = fs.readFileSync(
  path.join(nativeRoot, "tools", "uninstall_desktop_tile.ps1"),
  "utf8"
);
assert.match(uninstaller, /Remove-ItemProperty/);
assert.match(uninstaller, /Wait-Process/);
assert.doesNotMatch(uninstaller, /Remove-Item\s+'HKCU:[^\n]+Run'/);

const solution = fs.readFileSync(
  path.join(nativeRoot, "SparkDashDesktopTile.sln"),
  "utf8"
);
assert.match(solution, /Debug\|ARM64 = Debug\|ARM64/);
assert.match(solution, /Release\|ARM64 = Release\|ARM64/);
assert.doesNotMatch(solution, /\|x86|WidgetCore|WidgetProvider/);

const workflow = fs.readFileSync(
  path.join(root, ".github", "workflows", "platform-companions.yml"),
  "utf8"
);
assert.match(workflow, /publish_desktop_tile\.ps1/);
assert.match(workflow, /-Architecture arm64/);

const docs = fs.readFileSync(
  path.join(root, "docs", "windows-desktop-tile.md"),
  "utf8"
);
assert.match(docs, /no Microsoft Store/i);
assert.match(docs, /no Developer Mode/i);
assert.match(docs, /127\.0\.0\.1:5555/);
assert.match(docs, /every second/i);
assert.match(docs, /rolling 60-second generation-tokens-per-second sparkline/i);
assert.match(docs, /available LLM model identifier/i);
assert.match(docs, /row in red while GPU thermal throttling is active/i);

console.log("Windows desktop tile source artifacts verified");
