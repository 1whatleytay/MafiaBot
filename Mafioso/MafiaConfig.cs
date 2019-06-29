using System;

namespace Mafioso {
    public class MafiaConfig {
        public readonly MafiaConfigElement
            Mafia = new MafiaConfigElement(0),
            Doctor = new MafiaConfigElement(0),
            Detective = new MafiaConfigElement(0),
            Silencer = new MafiaConfigElement(0),
            Hunter = new MafiaConfigElement(0);

        public string GetDescription() {
            return $"**Mafia**: {Mafia.GetDescription()}\n" +
                   $"**Doctor**: {Doctor.GetDescription()}\n" +
                   $"**Detective**: {Detective.GetDescription()}\n" +
                   $"**Silencer**: {Silencer.GetDescription()}\n" +
                   $"**Hunter**: {Hunter.GetDescription()}\n";
        }

        public MafiaConfig() {
            Mafia = new MafiaConfigElement(1.0 / 6.0, true, true);
            Doctor = new MafiaConfigElement(1.0 / 6.0, true);
            Detective = new MafiaConfigElement(1.0 / 9.0, true, true);
            Silencer = new MafiaConfigElement(1.0 / 7.0, true);
            Hunter = new MafiaConfigElement(1.0 / 10.0, true);
        }

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
                    case "detective":
                    case "detectives":
                        Detective = element;
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
                    case "hunter":
                    case "hunters":
                        Hunter = element;
                        break;
                    default:
                        Console.WriteLine($"Ignored unknown config role {target}.");
                        break;
                }
            }
        }
    }
}