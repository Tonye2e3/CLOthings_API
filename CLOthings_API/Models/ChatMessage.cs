// 這個檔案是照專案原本用 EF Core Power Tools 對資料庫做 Scaffold 產生 Model 的風格手動寫的。
// 之後如果照 create_ChatMessage_table.sql 把資料表建到資料庫之後，再重新對資料庫做一次
// Scaffold，工具會自動長出一份跟這個檔案幾乎一樣的版本，屆時直接用工具產生的版本蓋掉這個
// 手動版就可以了。
//
// 注意：這裡故意沒有在 User.cs 加「這個使用者收過/傳過哪些訊息」的反向集合屬性
// （例如 User.SentChatMessage、User.ReceivedChatMessage），因為 User.cs 是隊友負責的
// Users 那個領域的檔案——只在 ChatMessage 這邊設 Sender／Receiver 兩個單向的導覽屬性，
// 不用去動到 User.cs，也一樣可以正常查詢、正常建立關聯，只是沒辦法反過來從
// User 物件直接拿到「這個人所有訊息」的清單（用不到，Controller 都是直接查 ChatMessage 表）。
#nullable disable
using System;

namespace CLOthings_API.Models;

public partial class ChatMessage
{
    public int ChatMessageId { get; set; }

    public int SenderId { get; set; }

    public int ReceiverId { get; set; }

    public string Content { get; set; }

    public string ImagePath { get; set; }

    public DateTimeOffset SentAt { get; set; }

    public bool IsRead { get; set; }

    public virtual User Sender { get; set; }

    public virtual User Receiver { get; set; }
}