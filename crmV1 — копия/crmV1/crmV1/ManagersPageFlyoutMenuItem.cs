using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace crmV1
{

    public class ManagersPageFlyoutMenuItem
    {
        public ManagersPageFlyoutMenuItem()
        {
            TargetType = typeof(ManagersPageFlyoutMenuItem);
        }
        public int Id { get; set; }
        public string Title { get; set; }

        public Type TargetType { get; set; }
    }
}