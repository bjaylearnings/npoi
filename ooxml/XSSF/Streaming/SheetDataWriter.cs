﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NPOI.OpenXmlFormats.Spreadsheet;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.Util;
using NPOI.XSSF.Model;
using NPOI.XSSF.UserModel;

namespace NPOI.XSSF.Streaming
{
    public class SheetDataWriter
    {
        private static POILogger logger = POILogFactory.GetLogger(typeof(SheetDataWriter));

        public FileInfo _fd;
        public BufferedStream _out;
        private int _rownum;
        private int _numberOfFlushedRows;
        private int _lowestIndexOfFlushedRows; // meaningful only of _numberOfFlushedRows>0
        private int _numberOfCellsOfLastFlushedRow; // meaningful only of _numberOfFlushedRows>0
        private int _numberLastFlushedRow = -1; // meaningful only of _numberOfFlushedRows>0

        /**
 * Table of strings shared across this workbook.
 * If two cells contain the same string, then the cell value is the same index into SharedStringsTable
 */
        private SharedStringsTable _sharedStringSource;

        public SheetDataWriter()
        {
            _fd = createTempFile();
            _out = createWriter(_fd);
        }
        public SheetDataWriter(SharedStringsTable sharedStringsTable) : this()
        {
            _sharedStringSource = sharedStringsTable;
        }
        /**
 * Create a temp file to write sheet data. 
 * By default, temp files are created in the default temporary-file directory
 * with a prefix "poi-sxssf-sheet" and suffix ".xml".  Subclasses can override 
 * it and specify a different temp directory or filename or suffix, e.g. <code>.gz</code>
 * 
 * @return temp file to write sheet data
 */
        //TODO: may want to use a different writer object
        public FileInfo createTempFile()
        {
            return TempFile.CreateTempFile("poi-sxssf-sheet", ".xml");
        }

        /**
         * Create a writer for the sheet data.
         * 
         * @param  fd the file to write to
         */
        public BufferedStream createWriter(FileInfo fd)
        {
            FileStream fos = new FileStream(fd.FullName, FileMode.Open, FileAccess.ReadWrite);
            FileStream decorated;
            try
            {
                //decorated = decorateOutputStream(fos);
            }
            catch (Exception e)
            {
                fos.Close();
                throw e;
            }
            //TODO: this is the decorate?

            return new BufferedStream(
                    new BinaryWriter(fos, Encoding.UTF8).BaseStream);
        }

        /**
         * Override this to translate (such as encrypt or compress) the file output stream
         * as it is being written to disk.
         * The default behavior is to to pass the stream through unmodified.
         *
         * @param fos  the stream to decorate
         * @return a decorated stream
         * @throws IOException
         * @see #decorateInputStream(FileInputStream)
         */
        //TODO: may not need to decorate in C#
        protected Stream decorateOutputStream(Stream fos)
        {
            return fos;
        }

        /**
         * flush and close the temp data writer. 
         * This method <em>must</em> be invoked before calling {@link #getWorksheetXMLInputStream()}
         */
        public void Close()
        {
            _out.Flush();
            _out.Close();
        }

        /**
         * @return a stream to read temp file with the sheet data
         */
        public Stream GetWorksheetXMLInputStream()
        {
            ;
            Stream fis = new FileStream(_fd.FullName, FileMode.Open, FileAccess.ReadWrite);
            try
            {
                return decorateInputStream(fis);
            }
            catch (IOException e)
            {
                fis.Close();
                throw e;
            }
        }

        /**
         * Override this to translate (such as decrypt or expand) the file input stream
         * as it is being read from disk.
         * The default behavior is to to pass the stream through unmodified.
         *
         * @param fis  the stream to decorate
         * @return a decorated stream
         * @throws IOException
         * @see #decorateOutputStream(FileOutputStream)
         */
        protected Stream decorateInputStream(Stream fis)
        {
            return fis;
        }



        protected void Finalize()
        {
            _fd.Delete();
            if (File.Exists(_fd.FullName))
            {
                logger.Log(POILogger.ERROR, "Can't delete temporary encryption file: " + _fd);
            }
            //TODO: accomplish whatever this does.
            //super.finalize();
        }

        /**
         * Write a row to the file
         *
         * @param rownum 0-based row number
         * @param row    a row
         */
        public void WriteRow(int rownum, SXSSFRow row)
        {
            if (_numberOfFlushedRows == 0)
                _lowestIndexOfFlushedRows = rownum;
            _numberLastFlushedRow = Math.Max(rownum, _numberLastFlushedRow);
            _numberOfCellsOfLastFlushedRow = row.getLastCellNum();
            _numberOfFlushedRows++;
            BeginRow(rownum, row);
            var cells = row.allCellsIterator();
            int columnIndex = 0;
            while (cells.hasNext())
            {
                writeCell(columnIndex++, cells.next());
            }
            endRow();
        }

        void BeginRow(int rownum, SXSSFRow row)
        {
            //TODO: make sure this isn't off.
            var text = Encoding.ASCII.GetBytes("<row r=\"" + (rownum + 1) + "\"");
            _out.Write(text, 0, text.Length);
            if (row.hasCustomHeight())
            {
                text = Encoding.ASCII.GetBytes(" customHeight=\"true\"  ht=\"" + row.getHeightInPoints() + "\"");
                _out.Write(text, 0, text.Length);
            }
            if (row.getZeroHeight())
            {
                text = Encoding.ASCII.GetBytes(" hidden=\"true\"");
                _out.Write(text, 0, text.Length);
            }
            if (row.isFormatted())
            {
                text = Encoding.ASCII.GetBytes(" s=\"" + row.getRowStyleIndex() + "\"");
                _out.Write(text, 0, text.Length);
                text = Encoding.ASCII.GetBytes(" customFormat=\"1\"");
                _out.Write(text, 0, text.Length);
            }
            if (row.getOutlineLevel() != 0)
            {
                text = Encoding.ASCII.GetBytes(" outlineLevel=\"" + row.getOutlineLevel() + "\"");
                _out.Write(text, 0, text.Length);
            }
            if (row.getHidden() != null)
            {
                text = Encoding.ASCII.GetBytes(" hidden=\"" + (row.getHidden() ? "1" : "0") + "\"");
                _out.Write(text, 0, text.Length);
            }
            if (row.getCollapsed() != null)
            {
                text = Encoding.ASCII.GetBytes(" collapsed=\"" + (row.getCollapsed() ? "1" : "0") + "\"");
                _out.Write(text, 0, text.Length);
            }

            text = Encoding.ASCII.GetBytes(">\n");
            _out.Write(text, 0, text.Length);
            this._rownum = rownum;
        }

        void endRow()
        {
            var text = Encoding.ASCII.GetBytes("</row>\n");
            _out.Write(text, 0, text.Length);
        }

        public void writeCell(int columnIndex, ICell cell)
        {
            if (cell == null)
            {
                return;
            }
            string cellRef = new CellReference(_rownum, columnIndex).FormatAsString();
            var text = Encoding.ASCII.GetBytes("<c r=\"" + cellRef + "\"");
            _out.Write(text, 0, text.Length);
            ICellStyle cellStyle = cell.CellStyle;
            if (cellStyle.Index != 0)
            {
                // need to convert the short to unsigned short as the indexes can be up to 64k
                // ideally we would use int for this index, but that would need changes to some more 
                // APIs
                text = Encoding.ASCII.GetBytes(" s=\"" + (cellStyle.Index & 0xffff) + "\"");
                _out.Write(text, 0, text.Length);
            }
            CellType cellType = cell.CellType;
            switch (cellType)
            {
                case CellType.Blank:
                    {
                        text = Encoding.ASCII.GetBytes(">");
                        _out.Write(text, 0, text.Length);
                        break;
                    }
                case CellType.Formula:
                    {
                        //TODO: I may have fucked this up. :)
                        text = Encoding.ASCII.GetBytes(">");
                        _out.Write(text, 0, text.Length);
                        text = Encoding.ASCII.GetBytes("<f>");
                        _out.Write(text, 0, text.Length);
                        outputQuotedString(cell.CellFormula);
                        text = Encoding.ASCII.GetBytes("</f>");
                        _out.Write(text, 0, text.Length);
                        switch (cell.GetCachedFormulaResultTypeEnum())
                        {
                            case CellType.Numeric:
                                double nval = cell.NumericCellValue;
                                if (!Double.IsNaN(nval))
                                {
                                    text = Encoding.ASCII.GetBytes("<v>" + nval + "</v>");
                                    _out.Write(text, 0, text.Length);
                                }
                                break;
                            default:
                                break;
                        }
                        break;
                    }
                case CellType.String:
                    {
                        if (_sharedStringSource != null)
                        {
                            XSSFRichTextString rt = new XSSFRichTextString(cell.StringCellValue);
                            int sRef = _sharedStringSource.AddEntry(rt.GetCTRst());


                            //TODO: is this supposed to be and s=\"
                            text = Encoding.ASCII.GetBytes(" t=\"" + ST_CellType.s.ToString() + "\">");
                            _out.Write(text, 0, text.Length);
                            text = Encoding.ASCII.GetBytes("<v>");
                            _out.Write(text, 0, text.Length);
                            text = Encoding.ASCII.GetBytes(sRef.ToString());
                            _out.Write(text, 0, text.Length);
                            text = Encoding.ASCII.GetBytes("</v>");
                            _out.Write(text, 0, text.Length);

                        }
                        else
                        {
                            text = Encoding.ASCII.GetBytes(" t=\"inlineStr\">");
                            _out.Write(text, 0, text.Length);
                            text = Encoding.ASCII.GetBytes("<is><t");
                            _out.Write(text, 0, text.Length);

                            if (hasLeadingTrailingSpaces(cell.StringCellValue))
                            {
                                text = Encoding.ASCII.GetBytes(" xml:space=\"preserve\"");
                                _out.Write(text, 0, text.Length);
                                //_out.write(" xml:space=\"preserve\"");
                            }
                            text = Encoding.ASCII.GetBytes(">");
                            _out.Write(text, 0, text.Length);
                            // _out.write(">");
                            outputQuotedString(cell.StringCellValue);
                            text = Encoding.ASCII.GetBytes("</t></is>");
                            _out.Write(text, 0, text.Length);
                            //_out.write("</t></is>");
                        }
                        break;
                    }
                case CellType.Numeric:
                    {
                        text = Encoding.ASCII.GetBytes("</t></is>");
                        _out.Write(text, 0, text.Length);
                        //_out.write(" t=\"n\">");
                        text = Encoding.ASCII.GetBytes("</t></is>");
                        _out.Write(text, 0, text.Length);
                        //_out.write("<v>" + cell.NumericCellValue + "</v>");
                        break;
                    }
                case CellType.Boolean:
                    {
                        text = Encoding.ASCII.GetBytes(" t=\"b\">");
                        _out.Write(text, 0, text.Length);
                        //_out.write(" t=\"b\">");
                        text = Encoding.ASCII.GetBytes("<v>" + (cell.BooleanCellValue ? "1" : "0") + "</v>");
                        _out.Write(text, 0, text.Length);
                       // _out.write("<v>" + (cell.BooleanCellValue ? "1" : "0") + "</v>");
                        break;
                    }
                case CellType.Error:
                    {
                        FormulaError error = FormulaError.ForInt(cell.ErrorCellValue);

                       // _out.write(" t=\"e\">");
                        text = Encoding.ASCII.GetBytes(" t=\"e\">");
                        _out.Write(text, 0, text.Length);
                        //_out.write("<v>" + error.String + "</v>");
                        text = Encoding.ASCII.GetBytes("<v>" + error.String + "</v>");
                        _out.Write(text, 0, text.Length);
                        break;
                    }
                default:
                    {
                        throw new InvalidOperationException("Invalid cell type: " + cellType);
                    }
            }
            //_out.write("</c>");
            text = Encoding.ASCII.GetBytes("</c>");
            _out.Write(text, 0, text.Length);
        }


        /**
         * @return  whether the string has leading / trailing spaces that
         *  need to be preserved with the xml:space=\"preserve\" attribute
         */
        bool hasLeadingTrailingSpaces(string str)
        {
            if (str != null && str.Length > 0)
            {
                char firstChar = str[0];
                char lastChar = str[str.Length - 1];
                return Character.isWhitespace(firstChar) || Character.isWhitespace(lastChar);
            }
            return false;
        }

        //Taken from jdk1.3/src/javax/swing/text/html/HTMLWriter.java
        protected void outputQuotedString(String s)
        {
            throw new NotImplementedException();
        //    if (s == null || s.Length == 0)
        //    {
        //        return;
        //    }

        //    char[]
        //chars = s.ToCharArray();
        //    int last = 0;
        //    int length = s.Length;
        //    for (int counter = 0; counter < length; counter++)
        //    {
        //        char c = chars[counter];
        //        switch (c)
        //        {
        //            case '<':
        //                if (counter > last)
        //                {
        //                    _out.write(chars, last, counter - last);
        //                }
        //                last = counter + 1;
        //                _out.write("&lt;");
        //                break;
        //            case '>':
        //                if (counter > last)
        //                {
        //                    _out.write(chars, last, counter - last);
        //                }
        //                last = counter + 1;
        //                _out.write("&gt;");
        //                break;
        //            case '&':
        //                if (counter > last)
        //                {
        //                    _out.write(chars, last, counter - last);
        //                }
        //                last = counter + 1;
        //                _out.write("&amp;");
        //                break;
        //            case '"':
        //                if (counter > last)
        //                {
        //                    _out.write(chars, last, counter - last);
        //                }
        //                last = counter + 1;
        //                _out.write("&quot;");
        //                break;
        //            // Special characters
        //            case '\n':
        //            case '\r':
        //                if (counter > last)
        //                {
        //                    _out.write(chars, last, counter - last);
        //                }
        //                _out.write("&#xa;");
        //                last = counter + 1;
        //                break;
        //            case '\t':
        //                if (counter > last)
        //                {
        //                    _out.write(chars, last, counter - last);
        //                }
        //                _out.write("&#x9;");
        //                last = counter + 1;
        //                break;
        //            case 0xa0:
        //                if (counter > last)
        //                {
        //                    _out.write(chars, last, counter - last);
        //                }
        //                _out.write("&#xa0;");
        //                last = counter + 1;
        //                break;
        //            default:
        //                // YK: XmlBeans silently replaces all ISO control characters ( < 32) with question marks.
        //                // the same rule applies to unicode surrogates and "not a character" symbols.
        //                if (c < ' ' || Character.isLowSurrogate(c) || Character.isHighSurrogate(c) ||
        //                        ('\uFFFE' <= c && c <= '\uFFFF'))
        //                {
        //                    if (counter > last)
        //                    {
        //                        _out.write(chars, last, counter - last);
        //                    }
        //                    _out.write('?');
        //                    last = counter + 1;
        //                }
        //                else if (c > 127)
        //                {
        //                    if (counter > last)
        //                    {
        //                        _out.write(chars, last, counter - last);
        //                    }
        //                    last = counter + 1;
        //                    // If the character is outside of ascii, write the
        //                    // numeric value.
        //                    _out.write("&#");
        //                    _out.write(((int)c).ToString());
        //                    _out.write(";");
        //                }
        //                break;
        //        }
        //    }
        //    if (last < length)
        //    {
        //        _out.write(chars, last, length - last);
        //    }
        }

        /**
         * Deletes the temporary file that backed this sheet on disk.
         * @return true if the file was deleted, false if it wasn't.
         */
        bool dispose()
        {
            bool ret;
            try
            {
                _out.Close();
            }
            finally
            {
                _fd.Delete();
                ret = File.Exists(_fd.FullName);
            }
            return ret;
        }
    }
}
