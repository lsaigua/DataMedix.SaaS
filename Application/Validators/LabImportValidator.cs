using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Application.Validators
{
    public class LabImportValidator
    {
        public  bool CedulaValida(string cedula)
        {
            if (string.IsNullOrWhiteSpace(cedula) || cedula.Length != 10)
                return false;

            if (!cedula.All(char.IsDigit))
                return false;

            int provincia = int.Parse(cedula.Substring(0, 2));
            if (provincia < 1 || provincia > 24)
                return false;

            int[] coef = { 2, 1, 2, 1, 2, 1, 2, 1, 2 };
            int suma = 0;

            for (int i = 0; i < 9; i++)
            {
                int val = int.Parse(cedula[i].ToString()) * coef[i];
                if (val >= 10) val -= 9;
                suma += val;
            }

            int digito = (10 - (suma % 10)) % 10;
            return digito == int.Parse(cedula[9].ToString());
        }
    }
}
