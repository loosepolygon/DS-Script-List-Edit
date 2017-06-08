Imports System.IO
Imports System.Text

Public Class frmScriptListEdit
    Private luainfoLayout As dgvLayout
    Private luagnlLayout As dgvLayout

    Dim dgvs() As DataGridView
    Dim layouts() As dgvLayout

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
        If str Is Nothing Then str = ""

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
        dgvs = {dgvLuainfo, dgvLuagnl}
        layouts = {luainfoLayout, luagnlLayout}

        For i = 0 To layouts.Count - 1
            Dim dgv = dgvs(i)
            Dim layout = layouts(i)

            dgv.Rows.Clear()
            dgv.Columns.Clear()

            For j = 0 To layout.fieldCount - 1
                dgv.Columns.Add(layout.fieldNames(j), layout.fieldNames(j))
                dgv.Columns(j).DefaultCellStyle.BackColor = layout.fieldBGColors(j)
                dgv.Columns(j).DefaultCellStyle.ForeColor = Color.Black
                dgv.Columns(j).SortMode = DataGridViewColumnSortMode.NotSortable
            Next
        Next

        For Each dgv In dgvs
            AddHandler dgv.KeyDown, AddressOf Me.onDgvKeyDown
        Next
    End Sub

    Private Sub btnOpen_Click(sender As Object, e As EventArgs) Handles btnOpen.Click
        bigEndian = False

        initDGVs()

        ' luainfo file

        bytes = File.ReadAllBytes(txtFile.Text)

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
            Dim offset2 = SIntFromFour(&H10 + &H10 * i + &H8)
            Dim unk1 = SIntFromFour(&H10 + &H10 * i + &HC)
            Dim scriptName = RawStrToStr(RawStrFromBytes(offset))

            Dim scriptName2 As String = ""
            If offset2 <> 0 Then
                scriptName2 = RawStrToStr(RawStrFromBytes(offset2))
            End If

            Dim partRow(luainfoLayout.fieldCount - 1) As String

            partRow(luainfoLayout.getFieldIndex("Name")) = scriptName
            partRow(luainfoLayout.getFieldIndex("NPC ID")) = npcID
            partRow(luainfoLayout.getFieldIndex("Name2")) = scriptName2
            partRow(luainfoLayout.getFieldIndex("Unk1")) = unk1
            dgvLuainfo.Rows.Add(partRow)
        Next

        ' luagnl file

        Dim luagnlFileName = txtFile.Text.Replace(".luainfo", ".luagnl")
        If File.Exists(luagnlFileName) = False Then
            Throw New ApplicationException("Cannot find associated luagnl file: " & luagnlFileName)
        End If

        bytes = File.ReadAllBytes(luagnlFileName)

        Dim strOffset = 0
        Dim idx = 0

        Do
            strOffset = SIntFromFour(idx * &H4)

            If strOffset = 0 Then
                Exit Do
            End If

            rawStr = RawStrFromBytes(strOffset)
            Dim functionName As String = RawStrToStr(rawStr)

            dgvLuagnl.Rows.Add({functionName})

            idx += 1
        Loop
    End Sub

    Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
        ' luainfo file

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
            Dim name2 As String = dgvLuainfo.Rows(i).Cells(luainfoLayout.getFieldIndex("Name2")).Value
            Dim unk1 As Integer = dgvLuainfo.Rows(i).Cells(luainfoLayout.getFieldIndex("Unk1")).Value

            If name Is Nothing Then name = ""
            If name2 Is Nothing Then name2 = ""

            WriteBytes(fs, Int32ToFourByte(npcID))
            WriteBytes(fs, Int32ToFourByte(fs.Length))

            Dim currOffset = fs.Position
            fs.Position = fs.Length
            WriteBytes(fs, Str2Bytes(name))
            fs.WriteByte(0)
            fs.Position = currOffset

            Dim offset2 As Integer = 0
            If name2.Length > 0 Then
                offset2 = fs.Length

                currOffset = fs.Position
                fs.Position = fs.Length
                WriteBytes(fs, Str2Bytes(name2))
                fs.WriteByte(0)
                fs.Position = currOffset
            End If

            WriteBytes(fs, Int32ToFourByte(offset2))
            WriteBytes(fs, Int32ToFourByte(unk1))
        Next

        fs.Position = fs.Length

        ' Probably not important, but the game's original files end in multiples of 16.
        While fs.Length Mod &H10 <> 0
            fs.WriteByte(0)
        End While

        fs.Close()

        ' luagnl file

        Dim luagnlFileName = txtFile.Text.Replace(".luainfo", ".luagnl")

        If File.Exists(luagnlFileName) And Not File.Exists(luagnlFileName & ".bak") Then
            bytes = File.ReadAllBytes(luagnlFileName)
            File.WriteAllBytes(luagnlFileName & ".bak", bytes)
        End If

        If File.Exists(luagnlFileName) Then File.Delete(luagnlFileName)
        fs = New IO.FileStream(luagnlFileName, IO.FileMode.CreateNew)

        Dim functionCnt = dgvLuagnl.Rows.Count
        Dim offsets = New List(Of Integer)
        fs.SetLength(functionCnt * &H4 + &H4)
        fs.Position = fs.Length

        For i = 0 To functionCnt - 1
            offsets.Add(fs.Length)
            WriteBytes(fs, Str2Bytes(dgvLuagnl.Rows(i).Cells(0).Value & vbNullChar))
        Next

        fs.Position = 0
        For i = 0 To offsets.Count - 1
            WriteBytes(fs, Int32ToFourByte(offsets(i)))
        Next

        WriteBytes(fs, Int32ToFourByte(0))

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
        luainfoLayout.add("Name2", "string", Color.White)
        luainfoLayout.add("Unk1", "i32", Color.LightGray)

        luagnlLayout = New dgvLayout
        luagnlLayout.add("Function Name", "string", Color.White)
    End Sub

    Private Function getCurrentDgv() As DataGridView
        Return dgvs(tabControlRoot.SelectedIndex)
    End Function


    Private Sub btnCopy_Click(sender As Object, e As EventArgs) Handles btnCopy.Click
        Dim dgv = getCurrentDgv()

        If dgv.Rows.Count = 0 Then
            Return
        End If

        copyEntry(dgv, dgv.SelectedCells(0).RowIndex)
    End Sub

    Sub copyEntry(ByRef dgv As DataGridView, rowidx As Integer)
        Dim row(dgv.Columns.Count - 1)
        For i = 0 To row.Count - 1
            row(i) = dgv.Rows(dgv.SelectedCells(0).RowIndex).Cells(i).FormattedValue
        Next
        dgv.Rows.Add(row)
    End Sub

    Private Sub btnDelete_Click(sender As Object, e As EventArgs) Handles btnDelete.Click
        Dim dgv = getCurrentDgv()

        If dgv.Rows.Count = 0 Then
            Return
        End If

        dgv.Rows.RemoveAt(dgv.SelectedCells(0).RowIndex)
    End Sub

    Private Sub btnMoveUp_Click(sender As Object, e As EventArgs) Handles btnMoveUp.Click
        Dim dgv = getCurrentDgv()

        If dgv.Rows.Count < 2 Then
            Return
        End If

        Dim rowIndex = dgv.SelectedCells(0).RowIndex

        If rowIndex = 0 Then
            Return
        End If

        Dim rowAbove As DataGridViewRow = dgv.Rows(rowIndex - 1)

        dgv.Rows.RemoveAt(rowIndex - 1)
        dgv.Rows.Insert(rowIndex, rowAbove)
    End Sub

    Private Sub btnMoveDown_Click(sender As Object, e As EventArgs) Handles btnMoveDown.Click
        Dim dgv = getCurrentDgv()

        If dgv.Rows.Count < 2 Then
            Return
        End If

        Dim rowIndex = dgv.SelectedCells(0).RowIndex

        If rowIndex = dgv.Rows.Count - 1 Then
            Return
        End If

        Dim rowBelow As DataGridViewRow = dgv.Rows(rowIndex + 1)

        dgv.Rows.RemoveAt(rowIndex + 1)
        dgv.Rows.Insert(rowIndex, rowBelow)
    End Sub

    Private Sub onDgvKeyDown(sender As Object, e As KeyEventArgs)
        If (e.Modifiers = Keys.Control AndAlso e.KeyCode = Keys.V) = False Then
            Return
        End If

        Dim o = CType(Clipboard.GetDataObject(), DataObject)
        If o.GetDataPresent(DataFormats.Text) = False Then
            Return
        End If
        Dim text = o.GetData(DataFormats.Text).ToString
        text = text.Replace(vbCr, "").TrimEnd(vbLf)
        Dim lines As String() = text.Split(vbLf)

        Dim sourceRows = New List(Of String())(lines.Length)
        Dim sourceMaxColumnCount = 0
        Dim sourceRowCount = lines.Length
        For i = 0 To lines.Length - 1
            Dim words = lines(i).Split(vbTab)
            sourceRows.Add(words)

            If words.Count > sourceMaxColumnCount Then
                sourceMaxColumnCount = words.Count
            End If
        Next

        Dim dgv = CType(sender, DataGridView)

        Dim cell As DataGridViewCell = dgv.SelectedCells(dgv.SelectedCells.Count - 1)
        Dim startColumn = cell.ColumnIndex
        Dim endColumn = startColumn + sourceMaxColumnCount - 1
        Dim startRow = cell.RowIndex
        Dim endRow = startRow + sourceRowCount - 1

        If endRow > dgv.RowCount - 1 Then
            endRow = dgv.RowCount - 1
        End If
        If endColumn > dgv.ColumnCount - 1 Then
            endColumn = dgv.ColumnCount - 1
        End If

        Dim destColumnCount = endColumn - startColumn + 1
        Dim destRowCount = endRow - startRow + 1

        For x = 0 To destColumnCount - 1
            For y = 0 To destRowCount - 1
                Dim newValue As String = ""
                If x < sourceRows(y).Count Then
                    newValue = sourceRows(y)(x)
                End If
                dgv.Rows(startRow + y).Cells(startColumn + x).Value = newValue
            Next
        Next
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
