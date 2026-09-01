#requires -Version 7.0
#requires -PSEdition Core

<#
.SYNOPSIS
    Stages Debian libVLC into the linux-x64 publish folder for the GitHub Flatpak.

.DESCRIPTION
    WVC210-only. MPEG/RTSP need libvlc inside the sandbox; host VLC is invisible
    to Flatpak. Copies libvlc, plugins, unversioned .so names, and ldd extras
    such as libidn.so.12 (Freedesktop 24.08 has libidn2 only) and Debian
    libavcodec/libavutil (the runtime libavcodec.so.61 is too old for the
    Debian VLC plugin's avcodec_get_supported_config) into libvlc/linux-x64,
    plus flatpak-native.env for the generic Flatpak launch script.
    Run after DotNetPublish and before FlatpakPack.
#>

if (-not (Get-Command Import-PluginDependency -ErrorAction SilentlyContinue)) {
    $srcDir = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
    $pluginSupportModulePath = Join-Path $srcDir 'modules/Engine/PluginSupport.psm1'
    if (Test-Path $pluginSupportModulePath -PathType Leaf) {
        Import-Module $pluginSupportModulePath -Force -Global -ErrorAction Stop
    }
}

function Add-LinuxLibVlcUnversionedCopies {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory
    )

    if (-not (Test-Path -LiteralPath $DestinationDirectory -PathType Container)) {
        return
    }

    foreach ($stem in @('libvlc', 'libvlccore')) {
        $unversioned = Join-Path $DestinationDirectory "$stem.so"
        if (Test-Path -LiteralPath $unversioned) {
            continue
        }

        $files = @(Get-ChildItem -LiteralPath $DestinationDirectory -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like "$stem.so.*" })
        $soname = @($files | Where-Object { $_.Name -match "^$([regex]::Escape($stem))\.so\.\d+$" } | Select-Object -First 1)
        $source = if ($soname.Count -gt 0) {
            $soname[0]
        }
        else {
            $files | Select-Object -First 1
        }

        if ($null -eq $source) {
            continue
        }

        Copy-Item -LiteralPath $source.FullName -Destination $unversioned -Force
    }
}

function Add-LinuxLibVlcHostRuntimeDeps {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory,

        [Parameter(Mandatory = $true)]
        [string]$SourceLibDirectory
    )

    if (-not (Test-Path -LiteralPath $SourceLibDirectory -PathType Container)) {
        return
    }

    foreach ($stem in @('libidn', 'libavcodec', 'libavutil', 'libavformat', 'libswscale', 'libswresample')) {
        Get-ChildItem -LiteralPath $SourceLibDirectory -Filter "$stem.so*" -ErrorAction SilentlyContinue |
            ForEach-Object {
                Copy-Item -LiteralPath $_.FullName -Destination $DestinationDirectory -Force
            }
    }
}

function Copy-HostLinuxLibVlcNativeLibs {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory
    )

    $pluginsDir = Join-Path $DestinationDirectory 'plugins'
    foreach ($libdir in @('/usr/lib/x86_64-linux-gnu', '/usr/lib64', '/usr/lib')) {
        $so5 = Join-Path $libdir 'libvlc.so.5'
        $so = Join-Path $libdir 'libvlc.so'
        if (-not ((Test-Path -LiteralPath $so5) -or (Test-Path -LiteralPath $so))) {
            continue
        }

        Get-ChildItem -LiteralPath $libdir -Filter 'libvlc.so*' -ErrorAction SilentlyContinue |
            ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $DestinationDirectory -Force }
        Get-ChildItem -LiteralPath $libdir -Filter 'libvlccore.so*' -ErrorAction SilentlyContinue |
            ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $DestinationDirectory -Force }
        Add-LinuxLibVlcHostRuntimeDeps -DestinationDirectory $DestinationDirectory -SourceLibDirectory $libdir
        $hostPlugins = Join-Path $libdir 'vlc/plugins'
        if (Test-Path -LiteralPath $hostPlugins -PathType Container) {
            Copy-Item -Path (Join-Path $hostPlugins '*') -Destination $pluginsDir -Recurse -Force
            foreach ($extra in @('gui', 'vaapi', 'vdpau')) {
                $dir = Join-Path $pluginsDir $extra
                if (Test-Path -LiteralPath $dir -PathType Container) {
                    Remove-Item -LiteralPath $dir -Recurse -Force
                }
            }

            $vo = Join-Path $pluginsDir 'video_output'
            if (Test-Path -LiteralPath $vo -PathType Container) {
                Get-ChildItem -LiteralPath $vo -File -ErrorAction SilentlyContinue |
                    Where-Object { $_.Name -match '^lib(egl_|gl_|glx_|gles2_|xcb_|glconv_|wl_|xdg_shell_)' } |
                    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
            }

            $cache = Join-Path $pluginsDir 'plugins.dat'
            if (Test-Path -LiteralPath $cache -PathType Leaf) {
                Remove-Item -LiteralPath $cache -Force
            }
        }

        Add-LinuxLibVlcUnversionedCopies -DestinationDirectory $DestinationDirectory
        return $true
    }

    return $false
}

function Copy-LinuxLibVlcNativeLibs {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory,

        [Parameter(Mandatory = $false)]
        [string]$WslDistro = 'Debian'
    )

    New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null
    $pluginsDir = Join-Path $DestinationDirectory 'plugins'
    New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null

    if ($IsLinux) {
        $copied = Copy-HostLinuxLibVlcNativeLibs -DestinationDirectory $DestinationDirectory
        if (-not $copied) {
            throw 'StageLinuxLibVlc: libvlc was not found under /usr/lib (install libvlc5 / vlc-plugin-base).'
        }

        return
    }

    if (-not $IsWindows) {
        throw 'StageLinuxLibVlc requires Linux or Windows WSL.'
    }

    Assert-WslDistroAllowedForFlatpak -Distro $WslDistro
    $destUnix = ConvertTo-WslMountPath -WindowsPath $DestinationDirectory
    $script = @"
set -e
DEST=$(ConvertTo-ShSingleQuoted -Value $destUnix)
mkdir -p "`$DEST/plugins"
copy_libvlc() {
  local libdir
      for libdir in /usr/lib/x86_64-linux-gnu /usr/lib64 /usr/lib; do
    if [ -e "`$libdir/libvlc.so.5" ] || [ -e "`$libdir/libvlc.so" ]; then
      cp -a "`$libdir"/libvlc.so* "`$DEST/" 2>/dev/null || true
      cp -a "`$libdir"/libvlccore.so* "`$DEST/" 2>/dev/null || true
      for name in libavcodec libavutil libavformat libswscale libswresample; do
        cp -a "`$libdir"/`$name.so* "`$DEST/" 2>/dev/null || true
      done
      if [ -d "`$libdir/vlc/plugins" ]; then
        cp -a "`$libdir/vlc/plugins/." "`$DEST/plugins/"
      fi
      rm -rf "`$DEST/plugins/gui" "`$DEST/plugins/vaapi" "`$DEST/plugins/vdpau" 2>/dev/null || true
      rm -f "`$DEST/plugins/video_output"/libegl_* "`$DEST/plugins/video_output"/libgl_* "`$DEST/plugins/video_output"/libglx_* "`$DEST/plugins/video_output"/libgles2_* "`$DEST/plugins/video_output"/libxcb_* "`$DEST/plugins/video_output"/libglconv_* "`$DEST/plugins/video_output"/libwl_* "`$DEST/plugins/video_output"/libxdg_shell_* 2>/dev/null || true
      rm -f "`$DEST/plugins/plugins.dat" 2>/dev/null || true
      for stem in libvlc libvlccore; do
        if [ ! -e "`$DEST/`$stem.so" ]; then
          target=`$(ls -1 "`$DEST/`$stem.so".[0-9] 2>/dev/null | head -1)
          if [ -n "`$target" ]; then
            cp -L "`$target" "`$DEST/`$stem.so" 2>/dev/null || cp -a "`$target" "`$DEST/`$stem.so"
          fi
        fi
      done
      return 0
    fi
  done
  return 1
}
copy_ldd_deps() {
  local pass bin so base real
  for pass in 1 2 3 4 5 6; do
    for bin in "`$DEST"/lib*.so*; do
      if [ ! -e "`$bin" ]; then
        continue
      fi
      while read -r so; do
        if [ -z "`$so" ] || [ ! -e "`$so" ]; then
          continue
        fi
        base=`$(basename "`$so")
        case "`$base" in
          libc.so.*|libm.so.*|libpthread.so.*|libdl.so.*|librt.so.*|libresolv.so.*|ld-linux*.so.*|libgcc_s.so.*|libstdc++.so.*|libdbus-1.so.*|libsystemd.so.*|libcap.so.*|libglib-2.0.so.*|libgobject-2.0.so.*|libgio-2.0.so.*|libgmodule-2.0.so.*|libgthread-2.0.so.*|librsvg-2.so.*|libpango*|libcairo*|libgtk*|libgdk*|libatk*|libharfbuzz*|libX11.so.*|libxcb.so.*|libXext.so.*|libXrender.so.*|libXi.so.*|libGL.so.*|libEGL.so.*|libGLdispatch*|libGLX*|libpulse*|libasound*|libselinux*|libmount*|libblkid*|libffi.so.*|libpcre*|libxml2.so.*|libfontconfig*|libfreetype*|libpng*|libfribidi*)
            continue
            ;;
        esac
        if [ ! -e "`$DEST/`$base" ]; then
          cp -a "`$so" "`$DEST/"
        fi
        if [ -L "`$so" ]; then
          real=`$(readlink -f "`$so" || true)
          if [ -n "`$real" ] && [ -e "`$real" ] && [ ! -e "`$DEST/`$(basename "`$real")" ]; then
            cp -a "`$real" "`$DEST/"
          fi
        fi
      done < <(ldd "`$bin" 2>/dev/null | awk '/=> \// {print `$3}')
    done
  done
}
if ! copy_libvlc; then
  if command -v sudo >/dev/null 2>&1 && sudo -n true 2>/dev/null; then
    sudo apt-get update -qq
    sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq --no-install-recommends libvlc5 libvlccore9 libidn12 libavcodec61 libavutil59 libavformat61 libswscale8 libswresample5 vlc-plugin-base vlc-plugin-video-output
    copy_libvlc
  else
    echo "libvlc not installed in WSL distro; install libvlc5 vlc-plugin-base vlc-plugin-video-output" >&2
    exit 2
  fi
fi
copy_ldd_deps
"@
    $script = ConvertTo-UnixLineEndings -Value $script
    if (-not $script.EndsWith("`n")) {
        $script += "`n"
    }

    $tempScript = Join-Path ([System.IO.Path]::GetTempPath()) ("maksit-libvlc-" + [guid]::NewGuid().ToString('N') + ".sh")
    try {
        [System.IO.File]::WriteAllBytes($tempScript, [System.Text.UTF8Encoding]::new($false).GetBytes($script))
        $wslScript = ConvertTo-WslMountPath -WindowsPath $tempScript
        $wslOut = & wsl -d $WslDistro -- bash $wslScript 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "StageLinuxLibVlc WSL copy failed ($WslDistro): $wslOut"
        }
    }
    finally {
        if (Test-Path -LiteralPath $tempScript -PathType Leaf) {
            Remove-Item -LiteralPath $tempScript -Force -ErrorAction SilentlyContinue
        }
    }

    $hasLib = @(Get-ChildItem -LiteralPath $DestinationDirectory -Filter 'libvlc.so*' -ErrorAction SilentlyContinue)
    if ($hasLib.Count -eq 0) {
        throw "StageLinuxLibVlc copied no libvlc.so from WSL distro '$WslDistro'."
    }

    Add-LinuxLibVlcUnversionedCopies -DestinationDirectory $DestinationDirectory
}

function Write-FlatpakNativeEnvFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PublishDirectory
    )

    $envPath = Join-Path $PublishDirectory 'flatpak-native.env'
    $text = @"
export LD_LIBRARY_PATH="`$LIB/libvlc/linux-x64`${LD_LIBRARY_PATH:+:`$LD_LIBRARY_PATH}"
if [ -d "`$LIB/libvlc/linux-x64/plugins" ]; then
  export VLC_PLUGIN_PATH="`$LIB/libvlc/linux-x64/plugins"
fi
"@
    if (-not $text.EndsWith("`n")) {
        $text += "`n"
    }

    [System.IO.File]::WriteAllText($envPath, $text.Replace("`r`n", "`n"), [System.Text.UTF8Encoding]::new($false))
}

function Get-PluginMetadata {
    [pscustomobject]@{ mutatesRemote = $false }
}

function Invoke-Plugin {
    param(
        [Parameter(Mandatory = $true)]
        $Settings
    )

    Import-PluginDependency -ModuleName 'Logging' -RequiredCommand 'Write-Log'
    Import-PluginDependency -ModuleName 'DesktopPackSupport' -RequiredCommand 'Resolve-DesktopPublishDirectory'

    $pluginSettings = $Settings
    $sharedSettings = $Settings.context
    $scriptDir = $sharedSettings.scriptDir
    $runtimeIdentifier = [string](Get-PluginPropertyValue -PluginSettings $pluginSettings -Name 'runtimeIdentifier' -Default 'linux-x64')
    $publishDirSetting = [string](Get-PluginPropertyValue -PluginSettings $pluginSettings -Name 'publishDir')
    $wslDistro = [string](Get-PluginPropertyValue -PluginSettings $pluginSettings -Name 'wslDistro' -Default 'Debian')

    $publishDirectory = Resolve-DesktopPublishDirectory `
        -Context $sharedSettings `
        -RuntimeIdentifier $runtimeIdentifier `
        -PublishDir $publishDirSetting `
        -ScriptDir $scriptDir

    $vlcDest = Join-Path $publishDirectory 'libvlc\linux-x64'
    Write-Log -Level 'STEP' -Message "Staging Linux libVLC into $vlcDest"
    Copy-LinuxLibVlcNativeLibs -DestinationDirectory $vlcDest -WslDistro $wslDistro
    Write-FlatpakNativeEnvFile -PublishDirectory $publishDirectory
    Write-Log -Level 'OK' -Message "  Staged libVLC and flatpak-native.env for FlatpakPack."
}

Export-ModuleMember -Function Invoke-Plugin, Get-PluginMetadata
