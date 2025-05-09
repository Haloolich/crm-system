using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace crmV1
{
    public class RegistrationPageFlyoutMenuItem
    {
        public RegistrationPageFlyoutMenuItem()
        {
            TargetType = typeof(RegistrationPageFlyoutMenuItem);
        }
        public int Id { get; set; }
        public string Title { get; set; }

        public Type TargetType { get; set; }
    }
}