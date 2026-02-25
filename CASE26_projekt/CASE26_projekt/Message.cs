using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CASE26_projekt
{
     public enum MessageRoleType
     {
         System,
         User,
         Assistant
     }

     public class Message : INotifyPropertyChanged
     {
         private string _content = string.Empty;
         private DateTime _timestamp = DateTime.Now;
         private MessageRoleType _role;

         public string Content { get => _content; set { _content = value; OnPropertyChanged(); }}
         public DateTime Timestamp { get => _timestamp; set { _timestamp = value; OnPropertyChanged(); }}
         public MessageRoleType Role { get => _role; set { _role = value; OnPropertyChanged(); }}

         public event PropertyChangedEventHandler? PropertyChanged;
         private void OnPropertyChanged([CallerMemberName] string? name = null)
         {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
         }
     }
}
