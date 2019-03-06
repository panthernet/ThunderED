using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Newtonsoft.Json;
using ThunderED.API;
using ThunderED.Classes;
using ThunderED.Classes.Entities;
using ThunderED.Classes.Enums;
using ThunderED.Helpers;

namespace ThunderED.Modules.OnDemand
{
    public class CapsModule: AppModuleBase
    {
        public override LogCat Category => LogCat.Caps;

        private const long SKILL_CALIBR = 21611;

       /* private readonly List<SkillRequirement> DreadCommonSkills = new List<SkillRequirement>
        {
            new SkillRequirement(21611, "Jump Drive Calibration", 4), 
            new SkillRequirement(3456, "Jump Drive Operation"), 
            new SkillRequirement(22043, "Tactical Weapon Reconfiguration", 3, SkillType.Siege, 5), 
        };

        private readonly List<SkillRequirement> CarrierCommonSkills = new List<SkillRequirement>
        {
            new SkillRequirement(21611, "Jump Drive Calibration", 4), 
            new SkillRequirement(3456, "Jump Drive Operation"), 
            new SkillRequirement(24613, "Fighter Hangar Management"), 
            new SkillRequirement(40572, "Light Fighters", 3, SkillType.Weapon, 4), 
            new SkillRequirement(40573, "Support Fighters"), 
            new SkillRequirement(23069, "Fighters", 3, SkillType.Weapon, 5), 
        };

        private readonly List<SkillRequirement> LogiCommonSkills = new List<SkillRequirement>
        {
            new SkillRequirement(21611, "Jump Drive Calibration"), 
            new SkillRequirement(3456, "Jump Drive Operation"), 
            new SkillRequirement(27906, "Tactical Logistics Reconfiguration", 3, SkillType.Siege, 5), 
        };

        private readonly List<SkillRequirement> CapitalSkills = new List<SkillRequirement>
        {
            new SkillRequirement(20531, "Gallente Dreadnought", 3), 
            new SkillRequirement(21666, "Capital Hybrid Turret", 3), 
            new SkillRequirement(41405, "Capital Blaster Specialization", 1, SkillType.Weapon, 1), 
            new SkillRequirement(41406, "Capital Railgun Specialization", 1, SkillType.Weapon, 1), 
            new SkillRequirement(20525, "Amarr Dreadnought", 3), 
            new SkillRequirement(20327, "Capital Energy Turret", 3), 
            new SkillRequirement(41405, "Capital Pulse Laser Specialization", 1, SkillType.Weapon, 1), 
            new SkillRequirement(41408, "Capital Beam Laser Specialization", 1, SkillType.Weapon, 1), 
            new SkillRequirement(20532, "Minmatar Dreadnought", 3), 
            new SkillRequirement(21667, "Capital Projectile Turret", 3), 
            new SkillRequirement(41403, "Capital Autocannon Specialization", 1, SkillType.Weapon, 1), 
            new SkillRequirement(41404, "Capital Artillery Specialization", 1, SkillType.Weapon, 1), 
            new SkillRequirement(20530, "Caldari Dreadnought", 3), 
            new SkillRequirement(21668, "XL Torpedoes", 3), 
            new SkillRequirement(41409, "XL Torpedo Specialization", 1, SkillType.Weapon, 1), 
            new SkillRequirement(41410, "XL Cruise Missile Specialization", 1, SkillType.Weapon, 1), 

            new SkillRequirement(24313, "Gallente Carrier", 3), 

            new SkillRequirement(24311, "Amarr Carrier", 3), 

            new SkillRequirement(24314, "Minmatar Carrier", 3), 
            
            new SkillRequirement(24312, "Caldari Carrier", 3), 

            new SkillRequirement(24311, "Amarr Carrier", 3), 
            new SkillRequirement(24314, "Minmatar Carrier", 3), 
            new SkillRequirement(24313, "Gallente Carrier", 3), 
            new SkillRequirement(24312, "Caldari Carrier", 3), 

        };*/

        private bool _isWhoRunning;

        public class ShipsData
        {
            public Dictionary<string, ShipDataGroup> Groups = new Dictionary<string, ShipDataGroup>();
        }

        public class ShipDataGroup
        {
            public List<SkillRequirement> CommonSkills  = new List<SkillRequirement>();
            public Dictionary<string, List<SkillRequirement>> ShipSkills  = new Dictionary<string, List<SkillRequirement>>();
            public Dictionary<string, ShipSpecialCounter> SpecialCounters = new Dictionary<string, ShipSpecialCounter>();
        }

        public class ShipSpecialCounter
        {
            public long Id;
            public int Rank;
        }

        public class ShipResult
        {
            public string Text;
            public int Count;
            public string Name;
        }

        public async Task ProcessWhoCommand(ICommandContext context, string commandText)
        {
            if (_isWhoRunning)
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("capsCommandRunning"));
                return;
            }

            if (!File.Exists(SettingsManager.FileShipsData))
            {                
                await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("errFileNotFound"));
                return;
            }

            if(context.User.IsBot || string.IsNullOrEmpty(commandText))
                return;
            try
            {
                _isWhoRunning = true;
                var parts = commandText.Split(' ');
                var command = parts[0];
                var mod = parts.Length > 1 ? parts[1] : null;

                if ((!command.Equals("who", StringComparison.OrdinalIgnoreCase) && !command.Equals("all", StringComparison.OrdinalIgnoreCase) || (mod != null && !mod.Equals("online", StringComparison.OrdinalIgnoreCase)
                                                                                                                                                  && !mod.Equals("o", StringComparison.OrdinalIgnoreCase))))
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("helpCaps", SettingsManager.Settings.Config.BotDiscordCommandPrefix, "caps"));
                    return;
                }

                var isAll = command.Equals("all", StringComparison.OrdinalIgnoreCase);
                var isOnlineOnly = mod != null && (mod.Equals("online", StringComparison.OrdinalIgnoreCase) || mod.Equals("o", StringComparison.OrdinalIgnoreCase));


                var usersToCheck = new List<AuthUserEntity>();
                if (!isAll)
                {
                    foreach (var user in APIHelper.DiscordAPI.GetUsers(context.Channel.Id, isOnlineOnly))
                    {
                        var item = await SQLHelper.GetAuthUserByDiscordId(user.Id);
                        if (item != null && !string.IsNullOrEmpty(item.Data.Permissions) && SettingsManager.HasCharSkillsScope(item.Data.PermissionsList))
                            usersToCheck.Add(item);
                    }
                }
                else
                {
                    if (isOnlineOnly)
                    {
                        var dusers = APIHelper.DiscordAPI.GetUsers(0, true).Select(a=> a.Id);
                        usersToCheck = (await SQLHelper.GetAuthUsers((int) UserStatusEnum.Authed)).Where(item => dusers.Contains(item.DiscordId) &&
                                !string.IsNullOrEmpty(item.Data.Permissions) && SettingsManager.HasCharSkillsScope(item.Data.PermissionsList))
                            .ToList();
                    }
                    else
                        usersToCheck = (await SQLHelper.GetAuthUsers((int) UserStatusEnum.Authed)).Where(item =>
                                !string.IsNullOrEmpty(item.Data.Permissions) && SettingsManager.HasCharSkillsScope(item.Data.PermissionsList))
                            .ToList();
                }

                if (!usersToCheck.Any())
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("capsNoUsersFound"));
                    return;
                }

                var data = JsonConvert.DeserializeObject<ShipsData>(File.ReadAllText(SettingsManager.FileShipsData));
                if (data == null)
                {                
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("errFileContainsInvalidData"));
                    return;
                }

               /* var dredsDic = Enum.GetNames(typeof(DreadShipType)).ToDictionary(name => name, name => "");
                dredsDic.Remove(DreadShipType.None.ToString());
                var carrierDic = Enum.GetNames(typeof(CarrierShipType)).ToDictionary(name => name, name => "");
                carrierDic.Remove(CarrierShipType.None.ToString());
                var logiDic = Enum.GetNames(typeof(LogiShipType)).ToDictionary(name => name, name => "");
                logiDic.Remove(LogiShipType.None.ToString());
                var dreadCount = 0;
                var carrierCount = 0;
                var logiCount = 0;*/
                var totalUsers = usersToCheck.Count;
                var c5count = 0;

                var specialDic = new Dictionary<string, ShipSpecialCounter>();
                var dataDic = new Dictionary<string, List<ShipResult>>();
                var counts = new Dictionary<string, int>();

                foreach (var userEntity in usersToCheck)
                {
                    var token = await APIHelper.ESIAPI.RefreshToken(userEntity.RefreshToken, SettingsManager.Settings.WebServerModule.CcpAppClientId,
                        SettingsManager.Settings.WebServerModule.CcpAppSecret);
                    if (token == null)
                    {
                        await LogHelper.LogWarning($"Character {userEntity.Data.CharacterName}({userEntity.CharacterId}) has invalid token!");
                        continue;
                    }

                    var skills = await APIHelper.ESIAPI.GetCharSkills(Reason, userEntity.CharacterId, token);
                    if (skills?.skills == null || skills.skills.Count == 0) continue;
                    var calibr = skills.skills.FirstOrDefault(a => a.skill_id == SKILL_CALIBR)?.trained_skill_level ?? 0;
                    if (calibr >= 5)
                        c5count++;

                    foreach (var pair in data.Groups)
                    {
                        var groupName = pair.Key;
                        var group = pair.Value;

                        counts.Add(groupName, 0);

                        foreach (var valuePair in @group.SpecialCounters)
                        {
                            var counterName = valuePair.Key;
                            var counter = valuePair.Value;
                            //TODO
                        }

                        var isCommonSkills = group.CommonSkills.All(skill =>
                        {
                            var s = skills.skills.FirstOrDefault(a => a.skill_id == skill.Id);
                            if (s == null) return false;
                            return s.trained_skill_level >= skill.Minumim;
                        });

                        if (isCommonSkills)
                        {
                            var siegeValue = group.CommonSkills.Where(a => a.SkillType == SkillType.Siege).All(skill =>
                            {
                                var s = skills.skills.FirstOrDefault(a => a.skill_id == skill.Id);
                                if (s == null) return false;
                                return s.trained_skill_level >= skill.T2;
                            })
                                ? 2
                                : 1;

                            var groupCount = 0;
                            foreach (var valuePair in @group.ShipSkills)
                            {
                                var shipName = valuePair.Key;
                                var shipSkills = valuePair.Value;

                                var canDrive = shipSkills.All(skill =>
                                {
                                    var s = skills.skills.FirstOrDefault(a => a.skill_id == skill.Id);
                                    if (s == null) return false;
                                    if (skill.SkillType == SkillType.Weapon && skill.T2 > 0) return true;
                                    return s.trained_skill_level >= skill.Minumim;
                                });
                                if (!canDrive) continue;

                                var weapon = shipSkills.Where(a => a.SkillType == SkillType.Weapon).Any(skill =>
                                {
                                    var s = skills.skills.FirstOrDefault(a => a.skill_id == skill.Id);
                                    if (s == null) return false;
                                    return s.trained_skill_level >= skill.T2;
                                })
                                    ? 2
                                    : 1;

                                counts[groupName] += counts[groupName] + 1;
                                var result = new ShipResult
                                {
                                    Name = shipName,
                                    Text = $"{userEntity.Data.CharacterName}({calibr})[T{siegeValue},T{weapon}], "
                                };
                                if (!dataDic.ContainsKey(groupName))
                                    dataDic.Add(groupName, new List<ShipResult> {result});
                                else
                                {
                                    var sd = dataDic[groupName].FirstOrDefault(a => a.Name == result.Name);
                                    if (sd != null)
                                        sd.Text += result.Text;
                                    else dataDic[groupName].Add(result);
                                }
                            }
                        }

                    }
                }

                var split = dataDic.Sum(a => a.Value.Sum(b => b.Text.Length));
               /* var sb = new StringBuilder();
                sb.AppendLine(".");
                sb.AppendLine($"**Dreads** ({dreadCount} {LM.Get("of")} {totalUsers})");
                sb.AppendLine($"*Format: NAME (CALIBRATION) [SIEGE,WEAPON]*");
                sb.AppendLine($"```");
                foreach (var pair in dredsDic)
                {
                    sb.AppendLine($"{pair.Key} - {(string.IsNullOrEmpty(pair.Value) ? LM.Get("None") : pair.Value.Substring(0, pair.Value.Length - 2))}");
                }
                sb.AppendLine($"```");

                if (split)
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, sb.ToString());
                    sb = new StringBuilder();
                    sb.AppendLine(".");
                }

                sb.AppendLine($"**Carriers** ({carrierCount} {LM.Get("of")} {totalUsers})");
                sb.AppendLine($"*Format: NAME (CALIBRATION) [FIGHTERS]*");
                sb.AppendLine($"```");
                foreach (var pair in carrierDic)
                {
                    sb.AppendLine($"{pair.Key} - {(string.IsNullOrEmpty(pair.Value) ? LM.Get("None") : pair.Value.Substring(0, pair.Value.Length - 2))}");
                }
                sb.AppendLine($"```");

                if (split)
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, sb.ToString());
                    sb = new StringBuilder();
                    sb.AppendLine(".");
                }

                sb.AppendLine($"**Logi** ({logiCount} {LM.Get("of")} {totalUsers})");
                sb.AppendLine($"*Format: NAME (CALIBRATION) [SIEGE]*");
                sb.AppendLine($"```");
                foreach (var pair in logiDic)
                {
                    sb.AppendLine($"{pair.Key} - {(string.IsNullOrEmpty(pair.Value) ? LM.Get("None") : pair.Value.Substring(0, pair.Value.Length - 2))}");
                }
                sb.AppendLine($"```");

                if (split)
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, sb.ToString());
                    sb = new StringBuilder();
                    sb.AppendLine(".");
                }

                sb.AppendLine($"**{LM.Get("capsCalibr5")}**: {c5count} {LM.Get("of")} {totalUsers}");
                await APIHelper.DiscordAPI.ReplyMessageAsync(context, sb.ToString());
                */
            }
            finally
            {
                _isWhoRunning = false;
            }
        }
    }



    public class SkillRequirement
    {
        public string SkillName { get; set; }
        public long Id { get; set; }
        public int Minumim { get; set; }
        public int T2 { get; set; }
        public SkillType SkillType { get; set; }

        public SkillRequirement(long id, string name, int minimum = 1, SkillType type = SkillType.General, int t2 = 0)
        {
            SkillName = name;
            Id = id;
            Minumim = minimum;
            T2 = t2;
            SkillType = type;
        }

    }

    public enum SkillType
    {
        General,
        Siege,
        Weapon
    }

}
