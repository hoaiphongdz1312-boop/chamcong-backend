using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NguyenHoaiPhong_1681.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceGpsQr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CheckInDistanceMeters",
                table: "AttendanceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckInLatitude",
                table: "AttendanceRecords",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckInLongitude",
                table: "AttendanceRecords",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CheckOutDistanceMeters",
                table: "AttendanceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutLatitude",
                table: "AttendanceRecords",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutLongitude",
                table: "AttendanceRecords",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckInDistanceMeters",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckInLatitude",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckInLongitude",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckOutDistanceMeters",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckOutLatitude",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckOutLongitude",
                table: "AttendanceRecords");
        }
    }
}
