# Project PokeBot
![License](https://img.shields.io/badge/License-AGPLv3-blue.svg)
![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/hexbyt3/PokeBot/total?style=flat-square&logoColor=Red&color=red)

![image](https://github.com/user-attachments/assets/bee51b3d-92a0-482c-a4ad-7f9f08d21f51)

## Support Discord:

For support on setting up your own instance of PokeBot, feel free to join the discord!

[<img src="https://canary.discordapp.com/api/guilds/1369342739581505536/widget.png?style=banner2">](https://discord.gg/WRs22V6DgE)

[sys-botbase](https://github.com/olliz0r/sys-botbase) client for remote control automation of Nintendo Switch consoles.

# Screenshots
![sysbot](https://github.com/user-attachments/assets/dbc0f47f-c80b-4180-8918-6336ce0646c2)



# PokeBot Control Panel
- **Locally Hosted Control Panel** 
Control all of your bots with a simple to use control panel via `http://localhost:8080` on your host machine.

 ## Control Panel
<img width="1633" height="641" alt="image" src="https://github.com/user-attachments/assets/9e67d6d6-273a-4e2c-bf0e-ff7eb38b5ca8" />
<img width="1642" height="1095" alt="image" src="https://github.com/user-attachments/assets/762e41ce-0d66-4376-9019-9530a9360d80" />

 ## Remote Control
Control your switches right from the control center.  Simply open up the Remote Control window, select the IP of the switch you wish to control, and start clicking away on the remotes!
<img width="1405" height="1151" alt="image" src="https://github.com/user-attachments/assets/d92647c4-e177-4e19-97b2-34cfd26bb77e" />

 ## Log Viewer
View logs right from the control center!  Search for errors, users, and more!
<img width="1410" height="1160" alt="image" src="https://github.com/user-attachments/assets/aaf823a9-6709-49e8-8a82-52f6865cbf49" />

  ## Realtime feedback
 Control all of your programs with the click of a button!  Idle all, stop all, start all, turn on/off all your switch screens at once!
<img width="1037" height="640" alt="image" src="https://github.com/user-attachments/assets/42dd0998-a759-4739-b2c7-ba96d65124a9" />

- **Automatic Updates**
Update your bots with the click of a button to always stay current with latest PKHeX/ALM releases.
<img width="712" height="875" alt="image" src="https://github.com/user-attachments/assets/7fd0215b-c9a4-4d15-ac52-fb9d6a8de27c" />

# 📱 Access PokeBot from Any Device on Your Network

## Quick Setup

### 1. Enable Network Access (choose one):
- **Option A:** Right-click PokeBot.exe → Run as Administrator
- **Option B:** Run in admin cmd: `netsh http add urlacl url=http://+:8080/ user=Everyone`

### 2. Allow Through Firewall:
Run in admin cmd:
```cmd
netsh advfirewall firewall add rule name="PokeBot Web" dir=in action=allow protocol=TCP localport=8080
```

### 3. Connect From Your Phone:
- Get your PC's IP: `ipconfig` (look for IPv4 Address)
- On your phone: `http://YOUR-PC-IP:8080`
- Example: `http://192.168.1.100:8080`

## Requirements
- Same WiFi network
- Windows Firewall rule (step 2)
- Admin rights (first time only)

---

# Other Program Features

- Live Log Searching through the Log tab.  Search for anything and find results fast.

![image](https://github.com/user-attachments/assets/820d8892-ae52-4aa6-981a-cb57d1c32690)

- Tray Support - When you press X to close out of the program, it goes to the system tray.  Right click the PokeBot icon in the tray to exit or control the bot.

![image](https://github.com/user-attachments/assets/3a30b334-955c-4fb3-b7d8-60cd005a2e18)

# Pokémon Trading Bot Commands

## Core Trading Commands

| Command | Aliases | Description | Usage | Permissions |
|---------|---------|-------------|--------|-------------|
| `trade` | `t` | Trade a Pokémon from Showdown set or file | `.trade [code] <showdown_set>` or attach file | Trade Role |
| `hidetrade` | `ht` | Trade without showing embed details | `.hidetrade [code] <showdown_set>` or attach file | Trade Role |
| `batchTrade` | `bt` | Trade multiple Pokémon (max 3) | `.bt <sets_separated_by_--->` | Trade Role |
| `egg` | - | Trade an egg from provided Pokémon name | `.egg [code] <pokemon_name>` | Trade Role |

## Specialized Trading Commands

| Command | Aliases | Description | Usage | Permissions |
|---------|---------|-------------|--------|-------------|
| `dittoTrade` | `dt`, `ditto` | Trade Ditto with specific stats/nature | `.dt [code] <stats> <language> <nature>` | Public |
| `itemTrade` | `it`, `item` | Trade Pokémon holding requested item | `.it [code] <item_name>` | Public |
| `mysteryegg` | `me` | Trade random egg with perfect IVs | `.me [code]` | Public |
| `mysterymon` | `mm` | Trade random Pokémon with perfect stats | `.mm [code]` | Trade Role |

## Fix & Clone Commands

| Command | Aliases | Description | Usage | Permissions |
|---------|---------|-------------|--------|-------------|
| `fixOT` | `fix`, `f` | Fix OT/nickname if advert detected | `.fix [code]` | FixOT Role |
| `clone` | `c` | Clone the Pokémon you show | `.clone [code]` | Clone Role |
| `dump` | `d` | Dump the Pokémon you show | `.dump [code]` | Dump Role |

## Event & Battle-Ready Commands

| Command | Aliases | Description | Usage | Permissions |
|---------|---------|-------------|--------|-------------|
| `listevents` | `le` | List available event files | `.le [filter] [pageX]` | Public |
| `eventrequest` | `er` | Request specific event by index | `.er <index>` | Trade Role |
| `battlereadylist` | `brl` | List battle-ready files | `.brl [filter] [pageX]` | Public |
| `battlereadyrequest` | `brr`, `br` | Request battle-ready file by index | `.brr <index>` | Trade Role |
| `specialrequestpokemon` | `srp` | List/request wondercard events | `.srp <gen> [filter] [pageX]` or `.srp <gen> <index>` | Public/Trade Role |
| `geteventpokemon` | `gep` | Download event as pk file | `.gep <gen> <index> [language]` | Public |

## Queue & Status Commands

| Command | Aliases | Description | Usage | Permissions |
|---------|---------|-------------|--------|-------------|
| `tradeList` | `tl` | Show users in trade queue | `.tl` | Admin |
| `fixOTList` | `fl`, `fq` | Show users in FixOT queue | `.fl` | Admin |
| `cloneList` | `cl`, `cq` | Show users in clone queue | `.cl` | Admin |
| `dumpList` | `dl`, `dq` | Show users in dump queue | `.dl` | Admin |
| `medals` | `ml` | Show your trade count and medals | `.ml` | Public |

## Admin Commands

| Command | Aliases | Description | Usage | Permissions |
|---------|---------|-------------|--------|-------------|
| `tradeUser` | `tu`, `tradeOther` | Trade file to mentioned user | `.tu [code] @user` + attach file | Admin |

## Usage Notes

- **Code Parameter**: Optional trade code (8 digits). If not provided, a random code is generated.
- **Batch Trading**: Separate multiple sets with `---` in batch trades.
- **File Support**: Commands accept both Showdown sets and attached .pk files.
- **Permissions**: Different commands require different Discord roles for access.
- **Languages**: Supported languages for events include EN, JA, FR, DE, ES, IT, KO, ZH.

## Supported Games

- Sword/Shield (SWSH)
- Brilliant Diamond/Shining Pearl (BDSP) 
- Legends Arceus (PLA)
- Scarlet/Violet (SV)
- Let's Go Pikachu/Eevee (LGPE)
  

# License
Refer to the `License.md` for details regarding licensing.

---

# 🚀 Universal Bot Controller

This PokeBot project now supports the **Universal Bot Controller System**, enabling management of both PokeBot and RaidBot through a single web interface.

## ✨ Features

### 🔄 Master-Slave Architecture
- First started bot instance becomes the **Master** (Web server on port 8080)
- Additional instances are automatically detected as **Slaves**
- Works with any combination of PokeBot and RaidBot

### 🌐 Unified Web Interface
- Access via `http://localhost:8080/` regardless of which bot starts first
- Visually distinguishable bot types:
  - 🎮 **PokeBot** (green highlighted)
  - ⚔️ **RaidBot** (purple highlighted)
- Automatic bot type detection and display

### ⚡ Universal Commands
- **Start All** - Starts all bots of all types
- **Stop All** - Stops all bots of all types
- **Idle All** - Sets all bots to idle mode
- **Refresh Map All** - RaidBot-specific function (only available for RaidBot instances)

### 🔧 Bot-Specific Features
- Separate update systems for PokeBot and RaidBot from their respective repositories
- Bot-specific functions are automatically enabled/disabled
- Intelligent command routing based on bot type

## 🚀 Usage

### Single PokeBot:
1. Start `PokeBot.exe`
2. Open `http://localhost:8080/`
3. Manage your PokeBot instances

### Mixed Bot Environment:
1. Start any combination of PokeBot and RaidBot
2. The first started bot takes over the web server role
3. All subsequent bots are automatically detected
4. Manage all bots through a single interface at `http://localhost:8080/`

### Network Access for Universal Controller:
```cmd
# Admin permission for all bot types
netsh http add urlacl url=http://+:8080/ user=Everyone

# Firewall rule for Universal Controller
netsh advfirewall firewall add rule name="Universal Bot Controller" dir=in action=allow protocol=TCP localport=8080
```

## 🔧 Technical Details

### Port Standardization
- Both bot types now use **Port 8080/8081**
- Automatic collision detection and master-slave assignment
- Cross-bot communication via port files

### Bot Type Detection
- Automatic detection via Reflection:
  - PokeBot: `SysBot.Pokemon.Helpers.PokeBot`
  - RaidBot: `SysBot.Pokemon.SV.BotRaid.Helpers.SVRaidBot`

### Update Management
- PokeBot: Updates from PokeBot repository
- RaidBot: Updates from RaidBot repository
- Automatic repository detection based on bot type

The Universal Bot Controller System makes managing multiple bot instances of different types easier than ever! 🎉

---

# 🌐 Tailscale Multi-Node Management

Take your bot management to the next level with **Tailscale** integration! Manage bot instances across multiple computers/servers through a single web interface.

## ✨ What is Tailscale Integration?

Tailscale allows you to create a secure mesh network between your devices, enabling the Universal Bot Controller to manage bots running on different computers across the internet as if they were on the same local network.

### 🔑 Key Benefits
- **Remote Management**: Control bots on multiple computers from one dashboard
- **Secure**: Encrypted mesh network via Tailscale
- **Automatic Discovery**: Master node automatically finds and manages remote bots
- **Load Distribution**: Run bots on multiple machines for better performance
- **Geographic Distribution**: Place bots closer to different regions

## 🚀 Setup Guide

### 1. Install Tailscale
1. Install [Tailscale](https://tailscale.com/) on all computers you want to connect
2. Log in with the same Tailscale account on all devices
3. Note the Tailscale IP addresses of each device (usually `100.x.x.x`)

### 2. Configure Master Node
The **Master Node** runs the web dashboard and manages all other nodes:

```json
{
  "Hub": {
    "Tailscale": {
      "Enabled": true,
      "IsMasterNode": true,
      "MasterNodeIP": "100.x.x.x",
      "RemoteNodes": [
        "100.x.x.x",
        "100.x.x.x"
      ],
      "PortScanStart": 8081,
      "PortScanEnd": 8110,
      "PortAllocation": {
        "Enabled": true,
        "NodeAllocations": {
          "100.x.x.x": { "Start": 8081, "End": 8090 },
          "100.x.x.x": { "Start": 8091, "End": 8100 },
          "100.x.x.x": { "Start": 8101, "End": 8110 }
        }
      }
    }
  }
}
```

### 3. Configure Slave Nodes
**Slave Nodes** run bots and report to the master:

```json
{
  "Hub": {
    "Tailscale": {
      "Enabled": true,
      "IsMasterNode": false,
      "MasterNodeIP": "100.x.x.x",
      "RemoteNodes": [],
      "PortScanStart": 8091,
      "PortScanEnd": 8100
    }
  }
}
```

## ⚙️ Configuration Details

### Tailscale Settings

| Setting | Description | Master Node | Slave Node |
|---------|-------------|-------------|------------|
| `Enabled` | Enable Tailscale integration | `true` | `true` |
| `IsMasterNode` | Is this the master dashboard? | `true` | `false` |
| `MasterNodeIP` | Tailscale IP of master node | Own IP | Master's IP |
| `RemoteNodes` | List of slave node IPs | All slave IPs | `[]` (empty) |
| `PortScanStart` | Start of port scan range | `8081` | Your range start |
| `PortScanEnd` | End of port scan range | `8110` | Your range end |

### Port Allocation System

The port allocation system prevents conflicts when running multiple bot instances across nodes:

```json
"PortAllocation": {
  "Enabled": true,
  "NodeAllocations": {
    "100.x.x.x": { "Start": 8081, "End": 8090 },  // Master: 10 ports
    "100.x.x.x": { "Start": 8091, "End": 8100 },  // Slave 1: 10 ports  
    "100.x.x.x": { "Start": 8101, "End": 8110 }    // Slave 2: 10 ports
  }
}
```

## 🎯 Usage Examples

### Basic 2-Node Setup
**Computer 1 (Master)** - Main gaming PC:
- Tailscale IP: `100.x.x.x`
- Runs web dashboard on port 8080
- Manages 5 local bots on ports 8081-8085

**Computer 2 (Slave)** - Dedicated server:
- Tailscale IP: `100.x.x.x`  
- Runs 10 bots on ports 8091-8100
- No local web interface needed

**Access**: Go to `http://100.x.x.x:8080` from any device on your Tailscale network

### Advanced Multi-Node Setup
**Server Farm Configuration**:
- **Master Node**: Dashboard + 5 bots (ports 8081-8085)
- **Slave Node 1**: 10 PokeBot instances (ports 8091-8100)
- **Slave Node 2**: 10 RaidBot instances (ports 8101-8110)
- **Slave Node 3**: Additional capacity (ports 8111-8120)

## 🔧 Network Requirements

### Firewall Configuration
Each node needs appropriate firewall rules:

```cmd
# Allow Tailscale traffic (usually automatic)
# Allow bot TCP ports
netsh advfirewall firewall add rule name="Bot Ports" dir=in action=allow protocol=TCP localport=8081-8110

# Master node also needs web server port
netsh advfirewall firewall add rule name="Bot Dashboard" dir=in action=allow protocol=TCP localport=8080
```

### Port Planning
- **Web Dashboard**: 8080 (master node only)
- **Bot Instances**: 8081+ (configurable ranges per node)
- **Tailscale**: Automatic (usually UDP 41641)

## 📊 Dashboard Features

### Multi-Node View
The master dashboard displays all nodes with:
- **Node Status**: Online/Offline indicators
- **Bot Counts**: Total bots per node
- **Performance**: Individual bot statuses
- **Commands**: Send commands to specific nodes or all nodes

### Global Commands
- **Start All**: Starts bots on all connected nodes
- **Stop All**: Stops bots across the entire network
- **Update All**: Updates bot software on all nodes
- **Restart All**: Restarts all bot instances network-wide

## 🔍 Troubleshooting

### Common Issues

**Bots not discovered on remote nodes:**
1. Verify Tailscale connectivity: `ping 100.x.x.x`
2. Check firewall rules on target node
3. Ensure port ranges don't overlap
4. Verify JSON configuration syntax

**Connection timeouts:**
1. Check if ports are in use: `netstat -an | findstr :8081`
2. Verify bot is actually running on expected port
3. Test manual connection: `telnet 100.x.x.x 8081`

**Master node not starting:**
1. Ensure `IsMasterNode: true` is set
2. Check that port 8080 is available
3. Verify admin permissions for network access

### Debug Information
Enable verbose logging to troubleshoot:
- Check Windows Event Logs
- Monitor bot console output
- Use `telnet` to test TCP connectivity
- Verify Tailscale status with `tailscale status`

## 🚨 Security Considerations

### Network Security
- Tailscale provides encrypted mesh networking
- Only devices on your Tailscale network can access the dashboard
- Consider using Tailscale ACLs for additional security
- Regularly review connected devices in Tailscale admin panel

### Access Control
- Web dashboard has no built-in authentication
- Rely on Tailscale network-level security
- Consider additional reverse proxy with authentication if needed
- Monitor access logs for suspicious activity

The Tailscale integration transforms your bot network into a powerful distributed system! 🚀
