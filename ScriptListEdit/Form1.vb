Imports System.IO
Imports System.Text

Public Class frmScriptListEdit
    Private luainfoLayout As dgvLayout

    Private headerUnk1 As Integer
    Private headerUnk2 As Integer

    Public Shared bytes() As Byte
    Public Shared bigEndian As Boolean

    Private Sub txt_Drop(sender As Object, e As System.Windows.Forms.DragEventArgs) Handles txtFile.DragDrop
        Dim file() As String = e.Data.GetData(DataFormats.FileDrop)
        sender.Text = file(0)
    End Sub
    Private Sub txt_DragEnter(sender As Object, e As System.Windows.Forms.DragEventArgs) Handles txtFile.DragEnter
        e.Effect = DragDropEffects.Copy
    End Sub

    Private Sub WriteBytes(ByRef fs As FileStream, ByVal byt() As Byte)
        For i = 0 To byt.Length - 1
            fs.WriteByte(byt(i))
        Next
    End Sub

    Private Function RawStrFromBytes(ByVal loc As UInteger, Optional ByVal maxLength As Integer = Int32.MaxValue) As Byte()
        Dim cont As Boolean = True
        Dim len As Integer = 0

        While cont
            If bytes(loc + len) > 0 And len < maxLength Then
                len += 1
            Else
                cont = False
            End If
        End While

        If len = 0 Then
            Return {}
        End If

        Dim strBytesJIS(len - 1) As Byte
        Array.Copy(bytes, loc, strBytesJIS, 0, len)

        Return strBytesJIS
    End Function
    Private Function Str2Bytes(ByVal str As String) As Byte()
        Dim BArr() As Byte
        BArr = Encoding.GetEncoding("shift_jis").GetBytes(str)
        Return BArr
    End Function
    Private Function RawStrToStr(ByVal rawStr As Byte()) As String
        Dim enc1 = Encoding.GetEncoding("shift_jis")
        Dim enc2 = Encoding.Unicode
        Dim strBytesUnicode As Byte() = Encoding.Convert(enc1, enc2, rawStr)
        Dim str As String = Encoding.Unicode.GetString(strBytesUnicode)
        Return str
    End Function

    Private Function Int32ToFourByte(ByVal val As Integer) As Byte()
        If bigEndian Then
            Return ReverseFourBytes(BitConverter.GetBytes(Convert.ToInt32(val)))
        Else
            Return BitConverter.GetBytes(Convert.ToInt32(val))
        End If
    End Function

    Private Function ReverseFourBytes(ByVal byt() As Byte)
        Return {byt(3), byt(2), byt(1), byt(0)}
    End Function

    Private Function SIntFromFour(ByVal loc As UInteger) As Integer
        Dim tmpint As Integer = 0
        Dim bArray(3) As Byte

        For i = 0 To 3
            bArray(3 - i) = bytes(loc + i)
        Next
        If Not bigEndian Then bArray = ReverseFourBytes(bArray)
        tmpint = BitConverter.ToInt32(bArray, 0)
        Return tmpint
    End Function

    Private Sub initDGVs()
        Dim dgvs() As DataGridView = {dgvLuainfo}
        Dim layouts() As dgvLayout = {luainfoLayout}

        For i = 0 To layouts.Count - 1
            Dim dgv = dgvs(i)
            Dim layout = layouts(i)

            dgv.Rows.Clear()
            dgv.Columns.Clear()

            For j = 0 To luainfoLayout.fieldCount - 1
                dgv.Columns.Add(layout.fieldNames(j), layout.fieldNames(j))
                dgv.Columns(j).DefaultCellStyle.BackColor = layout.fieldBGColors(j)
                dgv.Columns(j).DefaultCellStyle.ForeColor = Color.Black
                dgv.Columns(j).SortMode = DataGridViewColumnSortMode.NotSortable
            Next
        Next
    End Sub

    Private Sub btnOpen_Click(sender As Object, e As EventArgs) Handles btnOpen.Click
        bytes = File.ReadAllBytes(txtFile.Text)

        bigEndian = False

        initDGVs()

        Dim rawStr As Byte() = RawStrFromBytes(&H0, 4)
        Dim signature As String = RawStrToStr(rawStr)

        If signature <> "LUAI" Then
            Throw New ApplicationException("Not a luainfo file")
        End If

        headerUnk1 = SIntFromFour(&H4)

        Dim scriptCount As Integer = SIntFromFour(&H8)

        headerUnk2 = SIntFromFour(&HC)

        For i = 0 To scriptCount - 1
            Dim npcID = SIntFromFour(&H10 + &H10 * i + &H0)
            Dim offset = SIntFromFour(&H10 + &H10 * i + &H4)
            Dim unk1 = SIntFromFour(&H10 + &H10 * i + &H8)
            Dim unk2 = SIntFromFour(&H10 + &H10 * i + &HC)
            Dim scriptName = RawStrToStr(RawStrFromBytes(offset))

            Dim partRow(luainfoLayout.fieldCount - 1) As String

            partRow(luainfoLayout.getFieldIndex("Name")) = scriptName
            partRow(luainfoLayout.getFieldIndex("NPC ID")) = npcID
            partRow(luainfoLayout.getFieldIndex("Unk1")) = unk1
            partRow(luainfoLayout.getFieldIndex("Unk2")) = unk2
            dgvLuainfo.Rows.Add(partRow)
        Next

    End Sub

    Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
        bytes = File.ReadAllBytes(txtFile.Text)
        If Not File.Exists(txtFile.Text & ".bak") Then
            File.WriteAllBytes(txtFile.Text & ".bak", bytes)
        End If

        If File.Exists(txtFile.Text) Then File.Delete(txtFile.Text)
        Dim fs As New IO.FileStream(txtFile.Text, IO.FileMode.CreateNew)

        Dim scriptCount As Integer = dgvLuainfo.Rows.Count

        WriteBytes(fs, Str2Bytes("LUAI"))
        WriteBytes(fs, Int32ToFourByte(headerUnk1))
        WriteBytes(fs, Int32ToFourByte(scriptCount))
        WriteBytes(fs, Int32ToFourByte(headerUnk2))

        Dim namesOffset = &H10 + scriptCount * &H10
        fs.SetLength(namesOffset)

        For i = 0 To scriptCount - 1
            Dim name As String = dgvLuainfo.Rows(i).Cells(luainfoLayout.getFieldIndex("Name")).Value
            Dim npcID As Integer = dgvLuainfo.Rows(i).Cells(luainfoLayout.getFieldIndex("NPC ID")).Value
            Dim unk1 As Integer = dgvLuainfo.Rows(i).Cells(luainfoLayout.getFieldIndex("Unk1")).Value
            Dim unk2 As Integer = dgvLuainfo.Rows(i).Cells(luainfoLayout.getFieldIndex("Unk2")).Value

            WriteBytes(fs, Int32ToFourByte(npcID))
            WriteBytes(fs, Int32ToFourByte(fs.Length))
            WriteBytes(fs, Int32ToFourByte(unk1))
            WriteBytes(fs, Int32ToFourByte(unk2))

            Dim currOffset = fs.Position
            fs.Position = fs.Length
            WriteBytes(fs, Str2Bytes(name))
            fs.WriteByte(0)
            fs.Position = currOffset
        Next

        fs.Position = fs.Length

        While fs.Length Mod &H10 <> 0
            fs.WriteByte(0)
        End While

        fs.Close()
        MsgBox("Save Complete.")
    End Sub

    Private Sub frmScriptListEdit_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        luainfoLayout = New dgvLayout
        luainfoLayout.add("Name", "string", Color.White)
        luainfoLayout.add("NPC ID", "i32", Color.White)
        luainfoLayout.add("Unk1", "i32", Color.LightGray)
        luainfoLayout.add("Unk2", "i32", Color.LightGray)
    End Sub

End Class


Public Class dgvLayout
    Public fieldNames As List(Of String) = New List(Of String)
    Public fieldtypes As List(Of String) = New List(Of String)
    Public fieldBGColors As List(Of Color) = New List(Of Color)

    Public Sub add(ByVal name As String, ByVal type As String, Col As Color)
        fieldNames.Add(name)
        fieldtypes.Add(type)
        fieldBGColors.Add(Col)
    End Sub

    Public Function fieldCount() As Integer
        Return fieldNames.Count
    End Function
    Public Function getFieldIndex(ByVal name As String) As Integer
        Return fieldNames.IndexOf(name)
    End Function
End Class
