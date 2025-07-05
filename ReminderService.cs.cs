using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace LibraryEmailReminderWorker
{
    public static class ReminderService
    {
        public static async Task SendEmailReminders()
        {
            string connectionString = "Server=.;Database=P2Library;User Id=sa;Password=123456;TrustServerCertificate=true;";
            await SendSmsWithFast2SMS("9876543210", "Test SMS from Library Reminder");
            try
            {
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    string query = @"
                        SELECT 
                            p.Email,
                            p.Phone,
                            p.FName + ' ' + p.SName + ' ' + p.LName AS FullName,
                            b.Title,
                            br.DueDate
                        FROM BorrowingRecords br
                        INNER JOIN Person p ON br.PersonID = p.PersonID
                        INNER JOIN BookCopies bc ON br.CopyID = bc.CopyID
                        INNER JOIN Books b ON bc.BookID = b.BookID
                        LEFT JOIN Fines F ON br.BorrowingRecordID = F.BorrowingRecordID
                        WHERE br.ActualReturnDate IS NULL 
                          AND br.DueDate <= GETDATE() 
                          AND F.PaymentStatus IS NULL;";

                    SqlCommand cmd = new SqlCommand(query, con);
                    con.Open();
                    SqlDataReader reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        string? email = reader["Email"]?.ToString();
                        string? phone = reader["Phone"]?.ToString();
                        string? name = reader["FullName"]?.ToString() ?? "Unknown User";
                        string? title = reader["Title"]?.ToString() ?? "Untitled Book";
                        DateTime dueDate = reader["DueDate"] != DBNull.Value ? Convert.ToDateTime(reader["DueDate"]) : DateTime.MinValue;

                        string subject, body;

                        if (dueDate.Date == DateTime.Today.AddDays(1))
                        {
                            subject = "📚 Reminder: Book due tomorrow";
                            body = $"Dear {name},\n\nThe book \"{title}\" is due tomorrow ({dueDate:dd MMM yyyy}). Please return it on time.\n\n- Library System";
                        }
                        else if (dueDate.Date < DateTime.Today)
                        {
                            subject = "❗ Overdue Book Alert";
                            body = $"Dear {name},\n\nThe book \"{title}\" was due on {dueDate:dd MMM yyyy} and has not been returned.\nPlease return it as soon as possible.\n\n- Library System";
                        }
                        else
                        {
                            continue;
                        }

                        // ✅ Send Email
                        if (!string.IsNullOrWhiteSpace(email))
                        {
                            SendEmail(email, subject, body);
                        }

                        // ✅ Send SMS
                        if (!string.IsNullOrWhiteSpace(phone))
                        {
                            await SendSmsWithFast2SMS(phone, body);
                        }

                        File.AppendAllText("log.txt", $"[{DateTime.Now}] ✔ Notified {name} via Email/SMS\n");
                    }
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("log.txt", $"[{DateTime.Now}] ❌ ERROR in SendEmailReminders: {ex.Message}\n");
            }
        }

        public static async Task SendSmsWithFast2SMS(string phone, string message)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("authorization", "cM7Q8vI6pryzG5XqZ3VNfEtwkPOnHxBbSoaUFW0A1lR9DJKu2mqY1oicKjSd6kQUZRaGFLpwrDuvTt4A"); // 🔐 Replace this

                var values = new Dictionary<string, string>
                {
                    { "sender_id", "FSTSMS" },
                    { "message", message },
                    { "language", "english" },
                    { "route", "p" },
                    { "numbers", phone }
                };

                var content = new FormUrlEncodedContent(values);
                var response = await client.PostAsync("https://www.fast2sms.com/dev/bulkV2", content);
                var result = await response.Content.ReadAsStringAsync();

                File.AppendAllText("log.txt", $"[{DateTime.Now}] ✅ SMS API Response: {result}\n");

            }
            catch (Exception ex)
            {
                File.AppendAllText("log.txt", $"[{DateTime.Now}] ❌ ERROR sending SMS to {phone}: {ex.Message}\n");
            }
        }

        private static void SendEmail(string to, string subject, string body)
        {
            try
            {
                MailMessage mail = new MailMessage("hamsadhoch@gmail.com", to, subject, body);
                SmtpClient client = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential("hamsadhoch@gmail.com", "czssteskisqddthp"), // 🔐 Replace with your App Password
                    EnableSsl = true
                };

                client.Send(mail);

                File.AppendAllText("log.txt", $"[{DateTime.Now}] ✅ Email sent to {to}\n");
            }
            catch (Exception ex)
            {
                File.AppendAllText("log.txt", $"[{DateTime.Now}] ❌ ERROR sending email to {to}: {ex.Message}\n");
            }
        }
    }
}
