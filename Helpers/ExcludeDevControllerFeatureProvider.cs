using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using NguyenHoaiPhong_1681.Controllers;

namespace NguyenHoaiPhong_1681.Helpers;

/// <summary>Bỏ DevController khỏi danh sách controller (dùng ngoài Development).</summary>
public class ExcludeDevControllerFeatureProvider : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo) =>
        typeInfo.AsType() != typeof(DevController) && base.IsController(typeInfo);
}
