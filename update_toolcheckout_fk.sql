-- Change ToolCheckout FK from Employee to ApplicationUser
ALTER TABLE ToolCheckouts DROP CONSTRAINT FK_ToolCheckouts_Employees_CheckedOutById
GO

ALTER TABLE ToolCheckouts DROP CONSTRAINT FK_ToolCheckouts_Employees_CheckedInById  
GO

ALTER TABLE ToolCheckouts ADD CONSTRAINT FK_ToolCheckouts_AspNetUsers_CheckedOutById FOREIGN KEY (CheckedOutById) REFERENCES AspNetUsers(Id) ON DELETE RESTRICT
GO

ALTER TABLE ToolCheckouts ADD CONSTRAINT FK_ToolCheckouts_AspNetUsers_CheckedInById FOREIGN KEY (CheckedInById) REFERENCES AspNetUsers(Id) ON DELETE SET NULL
GO
