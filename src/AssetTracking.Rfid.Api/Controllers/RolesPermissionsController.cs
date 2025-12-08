using Microsoft.AspNetCore.Mvc;
using AssetTracking.Rfid.Api.Models;

namespace AssetTracking.Rfid.Api.Controllers;

[ApiController]
[Route("api/admin/roles/permissions")]
public class RolesPermissionsController : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<RolePermissions>> GetRolePermissions()
    {
        var list = new List<RolePermissions>
        {
            new RolePermissions
            {
                RoleName = "Super Admin",
                Modules = new()
                {
                    "Clients",
                    "SubscriptionSettings",
                    "YardsSites",
                    "SystemConfiguration",
                    "HardwareProvisioning",
                    "AllReports",
                    "AllAdmin"
                }
            },
            new RolePermissions
            {
                RoleName = "Client Admin",
                Modules = new()
                {
                    "UserManagement",
                    "EquipmentMaster",
                    "TrucksDrivers",
                    "GateDevices",
                    "Reports",
                    "AlertRules",
                    "Dashboard"
                }
            },
            new RolePermissions
            {
                RoleName = "Yard Supervisor",
                Modules = new()
                {
                    "LiveDashboard",
                    "CheckInOutApproval",
                    "Alerts",
                    "MissingEquipmentResolution",
                    "GateEvents"
                }
            },
            new RolePermissions
            {
                RoleName = "Driver",
                Modules = new()
                {
                    "MyTruck",
                    "MyEquipment",
                    "MyMissingItems"
                }
            },
            new RolePermissions
            {
                RoleName = "Auditor",
                Modules = new()
                {
                    "AuditLogs",
                    "Reports",
                    "ReaderHealth",
                    "ExceptionsHistory"
                }
            }
        };

        return Ok(list);
    }
}
