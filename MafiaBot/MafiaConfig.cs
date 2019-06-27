using System;
using System.Text;

namespace MafiaBot {
    public class MafiaConfigElement {
        private readonly double _number;
        private readonly bool _isPercentage;
        private readonly bool _roundUp;

        public int GetCount(int numberOfPlayers) {
            if (_isPercentage) {
                var number = _number * numberOfPlayers;
                
                if (_roundUp)
                    return (int) Math.Ceiling(number);
                
                return (int) Math.Floor(number);
            }

            return (int)_number;
        }

        public string GetDescription() {
            var postfix = _isPercentage ? "%" : "";
            var end = _isPercentage ? _roundUp ? " rounded up" : " rounded down" : "";
            var number = _isPercentage ? $"{_number * 100:N2}" : $"{(int) _number}";
            return $"{number}{postfix}{end}";
        }

        public MafiaConfigElement(double number, bool isPercentage = false, bool roundUp = false) {
            _number = number;
            _isPercentage = isPercentage;
            _roundUp = roundUp;
        }
        
        public MafiaConfigElement(string number) {
            var index = 0;

            if (number[index] == '+' || number[index] == '-') {
                _roundUp = number[index] == '+';
                index++;
            }

            var builder = new StringBuilder();
            while (index < number.Length && (char.IsDigit(number[index]) || number[index] == '.')) {
                builder.Append(number[index]);
                index++;
            }
            _number = double.Parse(builder.ToString());

            if (index < number.Length && number[index] == '%') {
                _number /= 100;
                _isPercentage = true;
                index++;
            }

            if (index < number.Length) Console.WriteLine($"Extra text at the end of {number}.");
        }
    }

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