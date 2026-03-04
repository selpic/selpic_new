namespace selpic_new
{
    class PrintJob
    {
        public long Id { get; set; }
        public string PrintType { get; set; } = "";
        public string GoodsType { get; set; } = "";
        public string PSize { get; set; } = "";
        public string PdfUrl { get; set; } = "";
        public string Color { get; set; } = "bw";
        public string Side { get; set; } = "single";
        public string Bind { get; set; } = "long";
        public int Copies { get; set; } = 1;
        public string PageRange { get; set; } = "all";
    }
}