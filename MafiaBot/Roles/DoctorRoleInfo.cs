namespace MafiaBot.Roles {
    public class DoctorRoleInfo {
        private bool _didHealSelfLast;

        public bool DidHealLast() {
            return _didHealSelfLast;
        }

        public void Heal(bool self) {
            _didHealSelfLast = self;
        }
    }
}