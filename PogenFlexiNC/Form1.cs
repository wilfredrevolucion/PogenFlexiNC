using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SqlClient;
using ClosedXML.Excel;
using System.Net;
using System.IO;
using SpreadsheetLight;

namespace PogenFlexiNC
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        SqlConnection conexionbd = new SqlConnection("Data Source = .; Initial Catalog = DIREKTOR_DATA; Integrated Security = True");
        private void Form1_Load(object sender, EventArgs e)
        {
            label3.Text = "Iniciando...";
            progressBar1.Value = 15; 

            DateTime diaAnterior = DateTime.Today.AddDays(-5);
            fecha1.Text = diaAnterior.ToString();
            DateTime fechaactual = DateTime.Today;
            fecha2.Text = fechaactual.ToString();

            conexionbd.Open();
            string queryftp = "select srv_nom, ftp_usr, ftp_pas, ftp_dir_fac, local_dir, nom_arch from inv_cad where substring(cod_emp, 1, 3) = '001' ";
            SqlCommand comando = new SqlCommand(queryftp, conexionbd);
            SqlDataReader leedato = comando.ExecuteReader();
            leedato.Read();

            string servidorftp = leedato.GetString(0);
            txtServidor.Text = servidorftp;
            string usuarioftp = leedato.GetString(1);
            txtUsuario.Text = usuarioftp;
            string contrasenaftp = leedato.GetString(2);
            txtPassword.Text = contrasenaftp;
            string directorioftp = leedato.GetString(3);
            txtDirFtp.Text = directorioftp;
            string directoriolocal = leedato.GetString(4);
            txtDirLocal.Text = directoriolocal;
            string nombrearchivo = leedato.GetString(5);
            txtArchivo.Text = nombrearchivo;

            conexionbd.Close();

            timer1.Start();
        }

        private void crearArchivo(Stream origen, Stream destino)
        {
            //este permite crear el archivo en el FTP
            byte[] buffer = new byte[1024];
            int bytesLeidos = origen.Read (buffer, 0, buffer.Length);
            while (bytesLeidos != 0)
            {
                destino .Write (buffer, 0, bytesLeidos);
                bytesLeidos = origen.Read (buffer, 0, buffer.Length);    
            }
            origen.Close ();
            destino.Close ();
        }

        private string GetNameFile()
        {
            string archivolog = "";
            archivolog = "log_"+DateTime .Now.Day + "_"+DateTime.Now.Month+"_"+DateTime.Now.Year;
            return archivolog;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            //este proceso hace el query a la base de datos para generar el reporte para el archivo
            label3.Text = "Consultando...";
            progressBar1 .Value = 35;

            string f1 = fecha1.Text;
            string f2 = fecha2.Text;

            f1 = fecha1.Value.ToString("yyyyMMdd");
            f2 = fecha2.Value.ToString("yyyyMMdd");

            string querydatos = "(SELECT (SELECT TOP 1 cod_flx FROM inv_cad WHERE cod_emp = '001') as TID, (SELECT TOP 1 nom_tda FROM inv_cad WHERE cod_emp = '001') as TIENDA,CONVERT(varchar, fac_enc_fac.fyh_nrd, 120) as FECHA,COUNT(DISTINCT(fac_enc_fac.num_doc)) as TICKETS,SUM(fac_enc_fac.mto) as VENTAS,(SELECT SUM(fac_det_fac.ctd) FROM fac_det_fac  WHERE fac_det_fac.id_doc = fac_enc_fac.id_doc)  as PIEZAS,10 as HORAS FROM fac_enc_fac WHERE cod_emp = '001' AND fac_enc_fac.mto > 0 AND fac_enc_fac.fec BETWEEN ' " + f1 + " ' and ' " + f2 + " ' GROUP BY fac_enc_fac.num_doc, fac_enc_fac.fyh_nrd, fac_enc_fac.id_doc) UNION ( SELECT (SELECT TOP 1 cod_flx FROM inv_cad WHERE cod_emp = '001') as TID, (SELECT TOP 1 nom_tda FROM inv_cad WHERE cod_emp = '001') as TIENDA, CONVERT(varchar, inv_encingbod.fyh_nrd, 120) as FECHA, COUNT(DISTINCT(inv_encingbod.num_doc)) as TICKETS, SUM(inv_encingbod.mto_tot_nc) as VENTAS, (SELECT SUM(inv_detingbod.ctd) FROM inv_detingbod  WHERE  inv_detingbod.id_doc = inv_encingbod.id_doc) as PIEZAS, 10 as HORAS FROM inv_encingbod WHERE cod_emp = '001' AND SUBSTRING(inv_encingbod.id_doc,10,3) = 'N1D' AND inv_encingbod.fec BETWEEN ' " + f1 + " ' and ' " + f2 + " ' GROUP BY inv_encingbod.num_doc, inv_encingbod.fyh_nrd, inv_encingbod.id_doc ) ";
            SqlDataAdapter adaptador = new SqlDataAdapter(querydatos, conexionbd);
            DataTable tabladatos = new DataTable();
            adaptador.Fill(tabladatos);

            dataGridView1.DataSource = tabladatos;

            timer1.Stop();
            timer2.Start();
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            //este proceso crea el archivo excel en directorio local
            label3.Text = "Creando archivo...";
            progressBar1.Value = 75;

            SLDocument documento = new SLDocument();

            documento.ImportDataTable(1, 1, (DataTable)dataGridView1.DataSource, true);
            documento.SaveAs(txtDirLocal.Text + txtArchivo.Text);
            label3.Text = "Archivo creado...";

            timer2.Stop();
            timer3.Start();
        }

        private void timer3_Tick(object sender, EventArgs e)
        {
            label3.Text = "Conectando FTP...";
            progressBar1.Value = 85;

            try
            {
                //conectar con servidor FTP
                Uri miuri = new Uri(txtServidor.Text + txtDirFtp.Text);
                FtpWebRequest clienteRequest = (FtpWebRequest)WebRequest.Create(miuri);
                NetworkCredential credenciales = new NetworkCredential();

                credenciales.UserName = txtUsuario.Text;
                credenciales.Password = txtPassword.Text;

                clienteRequest.Credentials = credenciales;
                clienteRequest.EnableSsl = false;
                clienteRequest.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
                clienteRequest.KeepAlive = true;
                clienteRequest.UsePassive = true;

                FtpWebResponse respuesta = (FtpWebResponse)clienteRequest.GetResponse();

                StreamReader sr = new StreamReader(respuesta.GetResponseStream(), Encoding.UTF8);
                string resultado = sr.ReadToEnd();
                richTextBox1.Text = respuesta.WelcomeMessage + "Conectado a FTP..." + "\r\n" + resultado;
                respuesta.Close();

                timer3.Stop();
                timer4.Start();
            }  
            catch (Exception ex) 
             {
                richTextBox1.Text = ex.Message ;

                string archivolog = GetNameFile();
                string cadena = "";
                cadena = DateTime .Now +"-"+ex.Message+Environment.NewLine;
                StreamWriter sw = new StreamWriter(@"C:\archivospogen\Errores\"+"/"+archivolog+".txt",true);
                sw.Write (cadena);
                sw.Close();

                timer3 .Stop ();
                timer4.Stop();
                this.Close ();

             }

        }

        private void timer4_Tick(object sender, EventArgs e)
        {
            label3.Text = "Subiendo archivo...";
            progressBar1 .Value = 95;


            try
            {
                //sube archivo a FTP
                //aca se indica el directorio del FTP donde se subirá el archivo
                Uri uri = new Uri(txtServidor.Text + txtDirFtp.Text + txtArchivo.Text);
                FtpWebRequest clienteRequest = (FtpWebRequest)WebRequest.Create(uri);
                NetworkCredential credenciales = new NetworkCredential();

                credenciales.UserName = txtUsuario.Text;
                credenciales.Password = txtPassword.Text;

                clienteRequest.Credentials = credenciales;
                clienteRequest.EnableSsl = false;
                clienteRequest.Method = WebRequestMethods.Ftp.UploadFile;
                clienteRequest.KeepAlive = true;
                clienteRequest.UsePassive = true;

                Stream destino = clienteRequest.GetRequestStream();
                //aca se indica el directorio del archivo a enviar al FTP
                FileStream origen = new FileStream(txtDirLocal.Text + txtArchivo.Text, FileMode.Open, FileAccess.Read);
                crearArchivo(origen, destino);

                progressBar1.Value = 100;
                label3.Text = "Fin proceso";

                timer4.Stop();
                this.Close();
            } 
            catch (Exception ex)
            {
                richTextBox1.Text = ex.Message;

                string archivolog = GetNameFile();
                string cadena = "";
                cadena = DateTime.Now + "-" + ex.Message + Environment.NewLine;
                StreamWriter sw = new StreamWriter(@"C:\archivospogen\Errores\" + "/" + archivolog + ".txt", true);
                sw.Write(cadena);
                sw.Close();

                timer4.Stop();
                this.Close();
            }

        }


    }
}
