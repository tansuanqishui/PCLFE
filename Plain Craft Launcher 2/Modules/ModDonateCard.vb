Imports System.Security.Cryptography
Imports System.Text

''' <summary>
''' 赞助解锁卡密（爱发电电铺自动发货）的离线校验。
''' 卡密格式：PCLFE-&lt;8 位序列号&gt;-&lt;Base64Url 编码的 ECDSA P-256 签名&gt;
''' 客户端仅内置公钥，私钥由作者本地保存，因此即使公开源代码也无法伪造卡密。
''' </summary>
Friend Module ModDonateCard

    'ECDSA P-256 公钥（CNG EccPublicBlob）
    Private ReadOnly CardPublicKey As Byte() = Convert.FromBase64String("RUNTMSAAAACguUErzKTHuTu6rwlY1VCQlX5m31lgT8N/X021pcrNvdoJ2ReoivHSeOuSZmmH9aVaG0Y+PhXKMqHMWYQzF4AH")

    ''' <summary>
    ''' 校验赞助卡密是否有效。
    ''' </summary>
    Friend Function VerifyDonateCard(Code As String) As Boolean
        If String.IsNullOrWhiteSpace(Code) Then Return False
        Try
            Dim Clean As String = Code.Replace(" ", "").Replace(vbCr, "").Replace(vbLf, "").Replace(vbTab, "").Trim()
            Dim Parts As String() = Clean.Split(New Char() {"-"c}, 3)
            If Parts.Length <> 3 Then Return False
            If Not Parts(0).Equals("PCLFE", StringComparison.OrdinalIgnoreCase) Then Return False
            Dim Serial As String = Parts(1).ToUpperInvariant()
            If Serial.Length <> 8 Then Return False

            Dim SigText As String = Parts(2).Replace("-"c, "+"c).Replace("_"c, "/"c)
            Select Case SigText.Length Mod 4
                Case 2
                    SigText &= "=="
                Case 3
                    SigText &= "="
            End Select
            Dim Signature As Byte() = Convert.FromBase64String(SigText)

            Dim Data As Byte() = Encoding.ASCII.GetBytes("PCLFE-THEME8-" & Serial)
            Using Key As CngKey = CngKey.Import(CardPublicKey, CngKeyBlobFormat.EccPublicBlob)
                Using Ecdsa As New ECDsaCng(Key)
                    Return Ecdsa.VerifyData(Data, Signature, HashAlgorithmName.SHA256)
                End Using
            End Using
        Catch ex As Exception
            Logger.Error(ex, "校验赞助卡密失败")
            Return False
        End Try
    End Function

End Module
