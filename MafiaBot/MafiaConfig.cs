using System;

namespace MafiaBot {
    public class MafiaConfig {
        public readonly MafiaConfigElement
            Mafia = new MafiaConfigElement(1.0 / 6.0, true, true),
            Doctor = new MafiaConfigElement(1.0 / 6.0, true),
            Investigator = new MafiaConfigElement(1.0 / 5.0, true),
            Silencer = new MafiaConfigElement(1.0 / 7.0, true);

        public string GetDescription() {
            return $"**Mafia**: {Mafia.GetDescription()}\n" +
                   $"**Doctor**: {Doctor.GetDescription()}\n" +
                   $"**Investigator**: {Investigator.GetDescription()}\n" +
                   $"**Silencer**: {Silencer.GetDescription()}\n";
        }

        public MafiaConfig() { }

        public MafiaConfig(string content) {
            var split = content.Replace(":", " ").Replace(",", " ").Split(" ");
            var index = 0;

            while (index < split.Length) {
                if (split[index].Length == 0) {
                    index++;
                    continue;
                }

                string target = null;
                string number = null;

                while ((target == null || number == null) && index < split.Length) {
                    if (split[index].Length != 0
                        && (char.IsDigit(split[index][0]) || split[index][0] == '+' || split[index][0] == '-')) {
                        number = split[index];
                    } else {
                        target = split[index].ToLower();
                    }
                    index++;
                }
                
                if (target == null || number == null) return;
                
                var element = new MafiaConfigElement(number);

                switch (target) {
                    case "mafias":
                    case "mafia":
                        Mafia = element;
                        break;
                    case "invest":
                    case "investigator":
                    case "investigators":
                        Investigator = element;
                        break;
                    case "doctor":
                    case "doctors":
                        Doctor = element;
                        break;
                    case "silence":
                    case "silencer":
                    case "silencers":
                        Silencer = element;
                        break;
                    default:
                        Console.WriteLine($"Ignored unknown config role {target}.");
                        break;
                }
            }
        }
    }
}