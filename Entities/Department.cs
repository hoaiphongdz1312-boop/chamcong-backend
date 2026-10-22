namespace NguyenHoaiPhong_1681.Entities;

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int? ManagerId { get; set; }
    public User? Manager { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Members { get; set; } = new List<User>();
}
