using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using WS.Business.Services;
using WS.Interfaces;

namespace WS.Web.Extensions
{
    public static class EmailSenderExtensions
    {
        public static Task SendEmailConfirmationAsync(this EmailSender emailSender, string email, string link)
        {
            return emailSender.SendEmailAsync(email, "Confirm your email",
                $"Please confirm your account by clicking this link: <a href='{link}'>link</a>");
        }
        public static Task SendEmailInvitationForSharedFileAsync(this EmailSender emailSender, string email, string name, string link)
        {
            return emailSender.SendEmailAsync(email, $"{name} has shared a file with you",
                $"{name} has shared the file with you via the following link {link}");
        }
    }
}
