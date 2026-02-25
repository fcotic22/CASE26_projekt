using System;
using System.Windows;
using System.Windows.Controls;

namespace CASE26_projekt
{
     public class ChatMessageTemplateSelector : DataTemplateSelector
     {
         public DataTemplate UserTemplate { get; set; }
         public DataTemplate AssistantTemplate { get; set; }

         public override DataTemplate SelectTemplate(object item, DependencyObject container)
         {
            if (item is Message message)
            {
               return message.Role == MessageRoleType.User ? UserTemplate : AssistantTemplate;
            }
         return base.SelectTemplate(item, container);
     }
 }
}
