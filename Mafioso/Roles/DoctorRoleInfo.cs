namespace Mafioso.Roles {
    public class DoctorRoleInfo {
        private ulong? _healedLast;

        public ulong? HealedLast() {
            return _healedLast;
        }

        public void Heal(ulong user) {
            _healedLast = user;
        }
    }
}