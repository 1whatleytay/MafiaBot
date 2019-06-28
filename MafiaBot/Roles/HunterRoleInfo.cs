namespace MafiaBot.Roles {
    public class HunterRoleInfo {
        private ulong? _personStalking;

        public bool IsStalking() {
            return _personStalking.HasValue;
        }

        public void Stalk(ulong user) {
            _personStalking = user;
        }

        public ulong? Stalkee() {
            return _personStalking;
        }

        public ulong? Kill() {
            var value = _personStalking;
            _personStalking = null;
            return value;
        }
    }
}