using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerApp
{
    public class Student
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string ClassName { get; set; } // Đổi tên thành ClassName để tránh trùng từ khóa 'class' của C#

        public Student(int id, string fullName, string className)
        {
            Id = id;
            FullName = fullName;
            ClassName = className;
        }

        // Ghi đè phương thức hiển thị để khi in ra chuỗi dễ nhìn hơn
        public override string ToString()
        {
            return $"[ID: {Id}] - {FullName} (Lớp: {ClassName})";
        }
    }
}
