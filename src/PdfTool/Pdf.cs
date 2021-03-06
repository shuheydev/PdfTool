﻿using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfTool.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PdfTool
{
    public class Pdf : IDisposable
    {
        private readonly PdfReader _pdfReader;
        private readonly TemporaryFile _tempFile;

        public Pdf(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"'{filePath}' not found.");
            }

            //temporary file will be automatically deleted.
            _tempFile = new TemporaryFile();
            string tempFilePath = _tempFile.FullName;

            File.Copy(filePath, tempFilePath, true);

            _pdfReader = new PdfReader(tempFilePath);
        }

        public void Write(string filePath)
        {
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                {
                    PdfStamper stamper = new PdfStamper(_pdfReader, fs);
                    stamper.Close();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        #region Rotate
        public int Rotate(int pageNumber, int rotateAngle)
        {
            //Make sure the pageNumber is in the valid range
            int pageCount = Count();
            if (pageNumber > pageCount || pageNumber < 1)
            {
                throw new ArgumentException($"Page number out of range. It must be (1-{pageCount})", "pageNumber");
            }

            //Make sure the rotateAngle is valid(0,90,180,270,-90,-180,-270).
            if (!IsAcceptableAngle(rotateAngle))
            {
                throw new ArgumentException($"Rotate angle is not acceptable. It must be ({string.Join(", ", _acceptableAngle)})", "rotateAngle");
            }

            //Rotate the page!
            var page = _pdfReader.GetPageN(pageNumber);
            page.Put(PdfName.Rotate, CalcRotatedObject(page, rotateAngle));

            //return angle after rotated.
            return page.GetAsNumber(PdfName.Rotate).IntValue;
        }

        private PdfNumber CalcRotatedObject(PdfDictionary page, int rotateAngle)
        {
            var rotate = page.GetAsNumber(PdfName.Rotate);

            return new PdfNumber(rotate == null ? rotateAngle : AdjustRotateAngle(rotate.IntValue + rotateAngle));
        }

        private int AdjustRotateAngle(int angle)
        {
            return angle % 360;
        }
        #endregion

        public int Count()
        {
            int pagesCount = _pdfReader.NumberOfPages;
            return pagesCount;
        }

        public int GetPageAngle(int pageNumber)
        {
            var page = _pdfReader.GetPageN(pageNumber);
            var angle = page.GetAsNumber(PdfName.Rotate);

            return angle is null ? 0 : angle.IntValue;
        }

        private static readonly int[] _acceptableAngle = { 0, 90, 180, 270, -0, -90, -180, -270 };

        public static bool IsAcceptableAngle(int angle)
        {
            if (_acceptableAngle.Contains(angle))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public static bool IsAcceptableAngle(string angle)
        {
            if (int.TryParse(angle, out int angleInt))
            {
                return IsAcceptableAngle(angleInt);
            }
            else
            {
                return false;
            }
        }

        public void Select(params int[] pageNumbers)
        {
            if (!pageNumbers.Any())
            {
                throw new ArgumentException("Page must be selected.", "pageNumbers");
            }

            int pageCount = Count();
            if (pageNumbers.Any(n => n > pageCount || n < 1))
            {
                throw new ArgumentException($"Page number out of range. It must be (1-{pageCount})", "pageNumbers");
            }

            _pdfReader.SelectPages(string.Join(",", pageNumbers));
        }

        public Pdf Merge(params Pdf[] pdfs)
        {
            if (!pdfs.Any())
            {
                throw new ArgumentException("Param need at least 1 Pdf object.", "pdfs");
            }

            var pageSize = GetPageSize(1);

            //Document -> PdfWriter
            var doc = new Document(pageSize);
            using var tempFile = new TemporaryFile();
            using var fs = new FileStream(tempFile.FullName, FileMode.Create);
            var writer = PdfWriter.GetInstance(doc, fs);
            doc.Open();

            var allPdfs = new List<Pdf>();
            allPdfs.Add(this);
            allPdfs.AddRange(pdfs);

            foreach (var pdf in allPdfs)
            {
                int pageCount = pdf.Count();
                var reader = pdf.GetReader();
                for (int i = 1; i <= pageCount; i++)
                {
                    //PdfWriter -> Document
                    var page = writer.GetImportedPage(reader, i);
                    doc.Add(Image.GetInstance(page));
                }
            }

            doc.Close();

            return new Pdf(tempFile.FullName);
        }

        public PdfDictionary GetPage(int pageNumber)
        {
            return _pdfReader.GetPageN(pageNumber);
        }

        public Rectangle GetPageSize(int pageNumber)
        {
            return _pdfReader.GetPageSize(pageNumber);
        }

        public PdfReader GetReader()
        {
            return _pdfReader;
        }

        public void Dispose()
        {
            _tempFile.Dispose();
            _pdfReader.Close();
        }
    }
}
