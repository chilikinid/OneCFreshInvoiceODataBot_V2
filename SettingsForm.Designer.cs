namespace OneCFreshInvoiceODataBot;

partial class SettingsForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel mainLayoutPanel;
    private TableLayoutPanel settingsFileLayoutPanel;
    private Label settingsFileLabel;
    private ComboBox settingsFileComboBox;
    private Button browseSettingsFileButton;
    private Label workingDirectoryLabel;
    private Label workingDirectoryPathLabel;
    private TabControl settingsTabControl;
    private TabPage processingTabPage;
    private TabPage oDataTabPage;
    private TabPage mailTabPage;
    private Panel processingSettingsPanel;
    private TableLayoutPanel processingTableLayoutPanel;
    private Label processingCommonModeLabel;
    private Label processingInputXlsxLabel;
    private TableLayoutPanel processingInputXlsxLayoutPanel;
    private TextBox processingInputXlsxTextBox;
    private Button browseInputXlsxButton;
    private Button openInputXlsxButton;
    private Label processingOutputDirLabel;
    private TextBox processingOutputDirTextBox;
    private Label processingODataMapPathLabel;
    private TextBox processingODataMapPathTextBox;
    private Label processingStopOnFirstErrorLabel;
    private CheckBox processingStopOnFirstErrorCheckBox;
    private Label processingDryRunLabel;
    private CheckBox processingDryRunCheckBox;
    private Label processingDownloadMetadataOnlyLabel;
    private CheckBox processingDownloadMetadataOnlyCheckBox;
    private Label processingSendEmailLabel;
    private CheckBox processingSendEmailCheckBox;
    private Label processingGeneratePrintFormsLabel;
    private CheckBox processingGeneratePrintFormsCheckBox;
    private Label processingCreateInvoicesOnlyLabel;
    private CheckBox processingCreateInvoicesOnlyCheckBox;
    private Label processingInvoiceFromDateLabel;
    private DateTimePicker processingInvoiceFromDateDateTimePicker;
    private Label processingRealizationDateLabel;
    private DateTimePicker processingRealizationDateDateTimePicker;
    private Label processingRealizationOnlyLabel;
    private Panel oDataSettingsPanel;
    private TableLayoutPanel oDataTableLayoutPanel;
    private Label oDataServiceRootLabel;
    private TextBox oDataServiceRootTextBox;
    private Label oDataLoginLabel;
    private TextBox oDataLoginTextBox;
    private Label oDataPasswordLabel;
    private TextBox oDataPasswordTextBox;
    private Label oDataTimeoutSecondsLabel;
    private NumericUpDown oDataTimeoutSecondsNumericUpDown;
    private Panel mailSettingsPanel;
    private TableLayoutPanel mailTableLayoutPanel;
    private Label mailSmtpHostLabel;
    private TextBox mailSmtpHostTextBox;
    private Label mailSmtpPortLabel;
    private NumericUpDown mailSmtpPortNumericUpDown;
    private Label mailUseStartTlsLabel;
    private CheckBox mailUseStartTlsCheckBox;
    private Label mailLoginLabel;
    private TextBox mailLoginTextBox;
    private Label mailPasswordLabel;
    private TextBox mailPasswordTextBox;
    private Label mailFromLabel;
    private TextBox mailFromTextBox;
    private Label mailResultEmailLabel;
    private TextBox mailResultEmailTextBox;
    private FlowLayoutPanel buttonPanel;
    private Button closeButton;
    private Button runButton;
    private Button saveAsButton;
    private Button saveButton;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        mainLayoutPanel = new TableLayoutPanel();
        settingsFileLayoutPanel = new TableLayoutPanel();
        settingsFileLabel = new Label();
        settingsFileComboBox = new ComboBox();
        browseSettingsFileButton = new Button();
        workingDirectoryLabel = new Label();
        settingsTabControl = new TabControl();
        processingTabPage = new TabPage();
        processingSettingsPanel = new Panel();
        processingTableLayoutPanel = new TableLayoutPanel();
        rbFromExcel = new RadioButton();
        processingInputXlsxLabel = new Label();
        processingInputXlsxLayoutPanel = new TableLayoutPanel();
        processingInputXlsxTextBox = new TextBox();
        browseInputXlsxButton = new Button();
        openInputXlsxButton = new Button();
        processingGeneratePrintFormsCheckBox = new CheckBox();
        processingCreateInvoicesOnlyCheckBox = new CheckBox();
        rbFrom1C = new RadioButton();
        processingInvoiceFromDateLabel = new Label();
        processingInvoiceFromDateDateTimePicker = new DateTimePicker();
        processingRealizationDateLabel = new Label();
        processingRealizationDateDateTimePicker = new DateTimePicker();
        processingCommonModeLabel = new Label();
        processingDownloadMetadataOnlyCheckBox = new CheckBox();
        processingStopOnFirstErrorCheckBox = new CheckBox();
        processingDryRunCheckBox = new CheckBox();
        processingInnLabel = new Label();
        processingInnTextBox = new TextBox();
        processingOutputDirLabel = new Label();
        processingOutputDirTextBox = new TextBox();
        processingODataMapPathLabel = new Label();
        processingODataMapPathTextBox = new TextBox();
        oDataTabPage = new TabPage();
        oDataSettingsPanel = new Panel();
        oDataTableLayoutPanel = new TableLayoutPanel();
        oDataServiceRootLabel = new Label();
        oDataServiceRootTextBox = new TextBox();
        oDataLoginLabel = new Label();
        oDataLoginTextBox = new TextBox();
        oDataPasswordLabel = new Label();
        oDataPasswordTextBox = new TextBox();
        oDataTimeoutSecondsLabel = new Label();
        oDataTimeoutSecondsNumericUpDown = new NumericUpDown();
        mailTabPage = new TabPage();
        mailSettingsPanel = new Panel();
        mailTableLayoutPanel = new TableLayoutPanel();
        processingSendEmailCheckBox = new CheckBox();
        mailSmtpHostLabel = new Label();
        mailSmtpHostTextBox = new TextBox();
        mailSmtpPortLabel = new Label();
        mailSmtpPortNumericUpDown = new NumericUpDown();
        mailUseStartTlsCheckBox = new CheckBox();
        mailLoginLabel = new Label();
        mailLoginTextBox = new TextBox();
        mailPasswordLabel = new Label();
        mailPasswordTextBox = new TextBox();
        mailFromLabel = new Label();
        mailResultEmailLabel = new Label();
        mailResultEmailTextBox = new TextBox();
        mailFromTextBox = new TextBox();
        buttonPanel = new FlowLayoutPanel();
        closeButton = new Button();
        runButton = new Button();
        saveAsButton = new Button();
        saveButton = new Button();
        processingStopOnFirstErrorLabel = new Label();
        processingDryRunLabel = new Label();
        processingDownloadMetadataOnlyLabel = new Label();
        processingSendEmailLabel = new Label();
        processingGeneratePrintFormsLabel = new Label();
        processingCreateInvoicesOnlyLabel = new Label();
        processingRealizationOnlyLabel = new Label();
        mailUseStartTlsLabel = new Label();
        workingDirectoryPathLabel = new Label();
        mainLayoutPanel.SuspendLayout();
        settingsFileLayoutPanel.SuspendLayout();
        settingsTabControl.SuspendLayout();
        processingTabPage.SuspendLayout();
        processingSettingsPanel.SuspendLayout();
        processingTableLayoutPanel.SuspendLayout();
        processingInputXlsxLayoutPanel.SuspendLayout();
        oDataTabPage.SuspendLayout();
        oDataSettingsPanel.SuspendLayout();
        oDataTableLayoutPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)oDataTimeoutSecondsNumericUpDown).BeginInit();
        mailTabPage.SuspendLayout();
        mailSettingsPanel.SuspendLayout();
        mailTableLayoutPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)mailSmtpPortNumericUpDown).BeginInit();
        buttonPanel.SuspendLayout();
        SuspendLayout();
        // 
        // mainLayoutPanel
        // 
        mainLayoutPanel.ColumnCount = 1;
        mainLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainLayoutPanel.Controls.Add(settingsFileLayoutPanel, 0, 0);
        mainLayoutPanel.Controls.Add(settingsTabControl, 0, 1);
        mainLayoutPanel.Controls.Add(buttonPanel, 0, 2);
        mainLayoutPanel.Dock = DockStyle.Fill;
        mainLayoutPanel.Location = new Point(0, 0);
        mainLayoutPanel.Name = "mainLayoutPanel";
        mainLayoutPanel.Padding = new Padding(12);
        mainLayoutPanel.RowCount = 3;
        mainLayoutPanel.RowStyles.Add(new RowStyle());
        mainLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayoutPanel.RowStyles.Add(new RowStyle());
        mainLayoutPanel.Size = new Size(860, 640);
        mainLayoutPanel.TabIndex = 0;
        // 
        // settingsFileLayoutPanel
        // 
        settingsFileLayoutPanel.AutoSize = true;
        settingsFileLayoutPanel.ColumnCount = 3;
        settingsFileLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
        settingsFileLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        settingsFileLayoutPanel.ColumnStyles.Add(new ColumnStyle());
        settingsFileLayoutPanel.Controls.Add(settingsFileLabel, 0, 0);
        settingsFileLayoutPanel.Controls.Add(settingsFileComboBox, 1, 0);
        settingsFileLayoutPanel.Controls.Add(browseSettingsFileButton, 2, 0);
        settingsFileLayoutPanel.Controls.Add(workingDirectoryLabel, 0, 1);
        settingsFileLayoutPanel.Dock = DockStyle.Fill;
        settingsFileLayoutPanel.Location = new Point(15, 15);
        settingsFileLayoutPanel.Name = "settingsFileLayoutPanel";
        settingsFileLayoutPanel.RowCount = 1;
        settingsFileLayoutPanel.RowStyles.Add(new RowStyle());
        settingsFileLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        settingsFileLayoutPanel.Size = new Size(830, 55);
        settingsFileLayoutPanel.TabIndex = 0;
        // 
        // settingsFileLabel
        // 
        settingsFileLabel.AutoSize = true;
        settingsFileLabel.Dock = DockStyle.Fill;
        settingsFileLabel.Location = new Point(3, 0);
        settingsFileLabel.Name = "settingsFileLabel";
        settingsFileLabel.Size = new Size(154, 35);
        settingsFileLabel.TabIndex = 0;
        settingsFileLabel.Text = "Файл настроек";
        settingsFileLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // settingsFileComboBox
        // 
        settingsFileComboBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        settingsFileComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        settingsFileComboBox.FormattingEnabled = true;
        settingsFileComboBox.Location = new Point(163, 6);
        settingsFileComboBox.MaxDropDownItems = 28;
        settingsFileComboBox.Name = "settingsFileComboBox";
        settingsFileComboBox.Size = new Size(544, 23);
        settingsFileComboBox.TabIndex = 1;
        settingsFileComboBox.SelectedIndexChanged += _SettingsFileComboBox_SelectedIndexChanged;
        // 
        // browseSettingsFileButton
        // 
        browseSettingsFileButton.AutoSize = true;
        browseSettingsFileButton.Location = new Point(713, 3);
        browseSettingsFileButton.MinimumSize = new Size(114, 29);
        browseSettingsFileButton.Name = "browseSettingsFileButton";
        browseSettingsFileButton.Size = new Size(114, 29);
        browseSettingsFileButton.TabIndex = 2;
        browseSettingsFileButton.Text = "Выбрать...";
        browseSettingsFileButton.UseVisualStyleBackColor = true;
        browseSettingsFileButton.Click += _BrowseSettingsFileButton_Click;
        // 
        // workingDirectoryLabel
        // 
        workingDirectoryLabel.AutoSize = true;
        workingDirectoryLabel.Dock = DockStyle.Fill;
        workingDirectoryLabel.Location = new Point(3, 35);
        workingDirectoryLabel.Name = "workingDirectoryLabel";
        workingDirectoryLabel.Size = new Size(154, 20);
        workingDirectoryLabel.TabIndex = 3;
        workingDirectoryLabel.Text = "Рабочая папка:";
        workingDirectoryLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // settingsTabControl
        // 
        settingsTabControl.Controls.Add(processingTabPage);
        settingsTabControl.Controls.Add(oDataTabPage);
        settingsTabControl.Controls.Add(mailTabPage);
        settingsTabControl.Dock = DockStyle.Fill;
        settingsTabControl.Location = new Point(15, 76);
        settingsTabControl.Name = "settingsTabControl";
        settingsTabControl.SelectedIndex = 0;
        settingsTabControl.Size = new Size(830, 499);
        settingsTabControl.TabIndex = 1;
        // 
        // processingTabPage
        // 
        processingTabPage.Controls.Add(processingSettingsPanel);
        processingTabPage.Location = new Point(4, 24);
        processingTabPage.Name = "processingTabPage";
        processingTabPage.Padding = new Padding(3);
        processingTabPage.Size = new Size(822, 471);
        processingTabPage.TabIndex = 0;
        processingTabPage.Text = "Processing";
        processingTabPage.UseVisualStyleBackColor = true;
        // 
        // processingSettingsPanel
        // 
        processingSettingsPanel.AutoScroll = true;
        processingSettingsPanel.Controls.Add(processingTableLayoutPanel);
        processingSettingsPanel.Dock = DockStyle.Fill;
        processingSettingsPanel.Location = new Point(3, 3);
        processingSettingsPanel.Name = "processingSettingsPanel";
        processingSettingsPanel.Padding = new Padding(8);
        processingSettingsPanel.Size = new Size(816, 465);
        processingSettingsPanel.TabIndex = 0;
        // 
        // processingTableLayoutPanel
        // 
        processingTableLayoutPanel.AutoSize = true;
        processingTableLayoutPanel.ColumnCount = 2;
        processingTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 249F));
        processingTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        processingTableLayoutPanel.Controls.Add(rbFromExcel, 0, 0);
        processingTableLayoutPanel.Controls.Add(processingInputXlsxLabel, 0, 1);
        processingTableLayoutPanel.Controls.Add(processingInputXlsxLayoutPanel, 1, 1);
        processingTableLayoutPanel.Controls.Add(processingGeneratePrintFormsCheckBox, 0, 2);
        processingTableLayoutPanel.Controls.Add(processingCreateInvoicesOnlyCheckBox, 1, 2);
        processingTableLayoutPanel.Controls.Add(rbFrom1C, 0, 3);
        processingTableLayoutPanel.Controls.Add(processingInvoiceFromDateLabel, 0, 4);
        processingTableLayoutPanel.Controls.Add(processingInvoiceFromDateDateTimePicker, 1, 4);
        processingTableLayoutPanel.Controls.Add(processingRealizationDateLabel, 0, 5);
        processingTableLayoutPanel.Controls.Add(processingRealizationDateDateTimePicker, 1, 5);
        processingTableLayoutPanel.Controls.Add(processingCommonModeLabel, 0, 6);
        processingTableLayoutPanel.Controls.Add(processingDownloadMetadataOnlyCheckBox, 0, 7);
        processingTableLayoutPanel.Controls.Add(processingStopOnFirstErrorCheckBox, 0, 8);
        processingTableLayoutPanel.Controls.Add(processingDryRunCheckBox, 1, 8);
        processingTableLayoutPanel.Controls.Add(processingInnLabel, 0, 9);
        processingTableLayoutPanel.Controls.Add(processingInnTextBox, 1, 9);
        processingTableLayoutPanel.Controls.Add(processingOutputDirLabel, 0, 10);
        processingTableLayoutPanel.Controls.Add(processingOutputDirTextBox, 1, 10);
        processingTableLayoutPanel.Controls.Add(processingODataMapPathLabel, 0, 11);
        processingTableLayoutPanel.Controls.Add(processingODataMapPathTextBox, 1, 11);
        processingTableLayoutPanel.Dock = DockStyle.Top;
        processingTableLayoutPanel.Location = new Point(8, 8);
        processingTableLayoutPanel.Name = "processingTableLayoutPanel";
        processingTableLayoutPanel.Padding = new Padding(0, 4, 16, 8);
        processingTableLayoutPanel.RowCount = 12;
        processingTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        processingTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        processingTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        processingTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        processingTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        processingTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        processingTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        processingTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        processingTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        processingTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        processingTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        processingTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        processingTableLayoutPanel.Size = new Size(800, 412);
        processingTableLayoutPanel.TabIndex = 0;
        // 
        // rbFromExcel
        // 
        rbFromExcel.AutoSize = true;
        processingTableLayoutPanel.SetColumnSpan(rbFromExcel, 2);
        rbFromExcel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        rbFromExcel.Location = new Point(3, 7);
        rbFromExcel.Name = "rbFromExcel";
        rbFromExcel.Size = new Size(117, 19);
        rbFromExcel.TabIndex = 32;
        rbFromExcel.TabStop = true;
        rbFromExcel.Text = "Чтение из Excel";
        rbFromExcel.UseVisualStyleBackColor = true;
        // 
        // processingInputXlsxLabel
        // 
        processingInputXlsxLabel.AutoSize = true;
        processingInputXlsxLabel.Location = new Point(3, 34);
        processingInputXlsxLabel.Name = "processingInputXlsxLabel";
        processingInputXlsxLabel.Size = new Size(65, 15);
        processingInputXlsxLabel.TabIndex = 0;
        processingInputXlsxLabel.Text = "Файл Excel";
        // 
        // processingInputXlsxLayoutPanel
        // 
        processingInputXlsxLayoutPanel.ColumnCount = 3;
        processingInputXlsxLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        processingInputXlsxLayoutPanel.ColumnStyles.Add(new ColumnStyle());
        processingInputXlsxLayoutPanel.ColumnStyles.Add(new ColumnStyle());
        processingInputXlsxLayoutPanel.Controls.Add(processingInputXlsxTextBox, 0, 0);
        processingInputXlsxLayoutPanel.Controls.Add(browseInputXlsxButton, 1, 0);
        processingInputXlsxLayoutPanel.Controls.Add(openInputXlsxButton, 2, 0);
        processingInputXlsxLayoutPanel.Dock = DockStyle.Fill;
        processingInputXlsxLayoutPanel.Location = new Point(249, 34);
        processingInputXlsxLayoutPanel.Margin = new Padding(0);
        processingInputXlsxLayoutPanel.Name = "processingInputXlsxLayoutPanel";
        processingInputXlsxLayoutPanel.RowCount = 1;
        processingInputXlsxLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        processingInputXlsxLayoutPanel.Size = new Size(535, 34);
        processingInputXlsxLayoutPanel.TabIndex = 1;
        // 
        // processingInputXlsxTextBox
        // 
        processingInputXlsxTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        processingInputXlsxTextBox.Location = new Point(0, 5);
        processingInputXlsxTextBox.Margin = new Padding(0, 5, 6, 5);
        processingInputXlsxTextBox.Name = "processingInputXlsxTextBox";
        processingInputXlsxTextBox.Size = new Size(351, 23);
        processingInputXlsxTextBox.TabIndex = 0;
        // 
        // browseInputXlsxButton
        // 
        browseInputXlsxButton.Anchor = AnchorStyles.Left;
        browseInputXlsxButton.AutoSize = true;
        browseInputXlsxButton.Location = new Point(357, 2);
        browseInputXlsxButton.Margin = new Padding(0, 0, 6, 0);
        browseInputXlsxButton.MinimumSize = new Size(38, 29);
        browseInputXlsxButton.Name = "browseInputXlsxButton";
        browseInputXlsxButton.Size = new Size(38, 29);
        browseInputXlsxButton.TabIndex = 1;
        browseInputXlsxButton.Text = "...";
        browseInputXlsxButton.UseVisualStyleBackColor = true;
        browseInputXlsxButton.Click += _BrowseInputXlsxButton_Click;
        // 
        // openInputXlsxButton
        // 
        openInputXlsxButton.Anchor = AnchorStyles.Left;
        openInputXlsxButton.AutoSize = true;
        openInputXlsxButton.Location = new Point(401, 2);
        openInputXlsxButton.Margin = new Padding(0);
        openInputXlsxButton.MinimumSize = new Size(134, 29);
        openInputXlsxButton.Name = "openInputXlsxButton";
        openInputXlsxButton.Size = new Size(134, 29);
        openInputXlsxButton.TabIndex = 2;
        openInputXlsxButton.Text = "Открыть в Excel";
        openInputXlsxButton.UseVisualStyleBackColor = true;
        openInputXlsxButton.Click += _OpenInputXlsxButton_Click;
        // 
        // processingGeneratePrintFormsCheckBox
        // 
        processingGeneratePrintFormsCheckBox.AutoSize = true;
        processingGeneratePrintFormsCheckBox.Location = new Point(3, 71);
        processingGeneratePrintFormsCheckBox.Name = "processingGeneratePrintFormsCheckBox";
        processingGeneratePrintFormsCheckBox.Size = new Size(202, 19);
        processingGeneratePrintFormsCheckBox.TabIndex = 17;
        processingGeneratePrintFormsCheckBox.Text = "Формировать печатные формы";
        processingGeneratePrintFormsCheckBox.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // processingCreateInvoicesOnlyCheckBox
        // 
        processingCreateInvoicesOnlyCheckBox.AutoSize = true;
        processingCreateInvoicesOnlyCheckBox.Location = new Point(252, 71);
        processingCreateInvoicesOnlyCheckBox.Name = "processingCreateInvoicesOnlyCheckBox";
        processingCreateInvoicesOnlyCheckBox.Size = new Size(155, 19);
        processingCreateInvoicesOnlyCheckBox.TabIndex = 19;
        processingCreateInvoicesOnlyCheckBox.Text = "Создавать только счета";
        // 
        // rbFrom1C
        // 
        rbFrom1C.AutoSize = true;
        processingTableLayoutPanel.SetColumnSpan(rbFrom1C, 2);
        rbFrom1C.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        rbFrom1C.Location = new Point(3, 105);
        rbFrom1C.Name = "rbFrom1C";
        rbFrom1C.Size = new Size(238, 19);
        rbFrom1C.TabIndex = 33;
        rbFrom1C.TabStop = true;
        rbFrom1C.Text = "Чтение существующих счетов из 1С";
        rbFrom1C.UseVisualStyleBackColor = true;
        // 
        // processingInvoiceFromDateLabel
        // 
        processingInvoiceFromDateLabel.AutoSize = true;
        processingInvoiceFromDateLabel.Location = new Point(3, 132);
        processingInvoiceFromDateLabel.Name = "processingInvoiceFromDateLabel";
        processingInvoiceFromDateLabel.Size = new Size(77, 15);
        processingInvoiceFromDateLabel.TabIndex = 20;
        processingInvoiceFromDateLabel.Text = "Счета с даты";
        // 
        // processingInvoiceFromDateDateTimePicker
        // 
        processingInvoiceFromDateDateTimePicker.Location = new Point(252, 135);
        processingInvoiceFromDateDateTimePicker.Name = "processingInvoiceFromDateDateTimePicker";
        processingInvoiceFromDateDateTimePicker.Size = new Size(200, 23);
        processingInvoiceFromDateDateTimePicker.TabIndex = 21;
        // 
        // processingRealizationDateLabel
        // 
        processingRealizationDateLabel.AutoSize = true;
        processingRealizationDateLabel.Location = new Point(3, 166);
        processingRealizationDateLabel.Name = "processingRealizationDateLabel";
        processingRealizationDateLabel.Size = new Size(100, 15);
        processingRealizationDateLabel.TabIndex = 22;
        processingRealizationDateLabel.Text = "Дата реализации";
        // 
        // processingRealizationDateDateTimePicker
        // 
        processingRealizationDateDateTimePicker.Location = new Point(252, 169);
        processingRealizationDateDateTimePicker.Name = "processingRealizationDateDateTimePicker";
        processingRealizationDateDateTimePicker.Size = new Size(200, 23);
        processingRealizationDateDateTimePicker.TabIndex = 23;
        // 
        // processingCommonModeLabel
        // 
        processingCommonModeLabel.AutoSize = true;
        processingTableLayoutPanel.SetColumnSpan(processingCommonModeLabel, 2);
        processingCommonModeLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        processingCommonModeLabel.Location = new Point(3, 200);
        processingCommonModeLabel.Name = "processingCommonModeLabel";
        processingCommonModeLabel.Size = new Size(49, 15);
        processingCommonModeLabel.TabIndex = 28;
        processingCommonModeLabel.Text = "Общие";
        // 
        // processingDownloadMetadataOnlyCheckBox
        // 
        processingDownloadMetadataOnlyCheckBox.AutoSize = true;
        processingTableLayoutPanel.SetColumnSpan(processingDownloadMetadataOnlyCheckBox, 2);
        processingDownloadMetadataOnlyCheckBox.Location = new Point(3, 237);
        processingDownloadMetadataOnlyCheckBox.Name = "processingDownloadMetadataOnlyCheckBox";
        processingDownloadMetadataOnlyCheckBox.Size = new Size(181, 19);
        processingDownloadMetadataOnlyCheckBox.TabIndex = 11;
        processingDownloadMetadataOnlyCheckBox.Text = "Только скачать метаданные";
        // 
        // processingStopOnFirstErrorCheckBox
        // 
        processingStopOnFirstErrorCheckBox.AutoSize = true;
        processingStopOnFirstErrorCheckBox.Location = new Point(3, 271);
        processingStopOnFirstErrorCheckBox.Name = "processingStopOnFirstErrorCheckBox";
        processingStopOnFirstErrorCheckBox.Size = new Size(204, 19);
        processingStopOnFirstErrorCheckBox.TabIndex = 7;
        processingStopOnFirstErrorCheckBox.Text = "Остановить при первой ошибке";
        // 
        // processingDryRunCheckBox
        // 
        processingDryRunCheckBox.AutoSize = true;
        processingDryRunCheckBox.Location = new Point(252, 271);
        processingDryRunCheckBox.Name = "processingDryRunCheckBox";
        processingDryRunCheckBox.Size = new Size(118, 19);
        processingDryRunCheckBox.TabIndex = 9;
        processingDryRunCheckBox.Text = "Пробный запуск";
        // 
        // processingInnLabel
        // 
        processingInnLabel.AutoSize = true;
        processingInnLabel.Location = new Point(3, 302);
        processingInnLabel.Name = "processingInnLabel";
        processingInnLabel.Size = new Size(108, 15);
        processingInnLabel.TabIndex = 30;
        processingInnLabel.Text = "ИНН организации";
        // 
        // processingInnTextBox
        // 
        processingInnTextBox.Location = new Point(252, 305);
        processingInnTextBox.Name = "processingInnTextBox";
        processingInnTextBox.Size = new Size(100, 23);
        processingInnTextBox.TabIndex = 31;
        // 
        // processingOutputDirLabel
        // 
        processingOutputDirLabel.AutoSize = true;
        processingOutputDirLabel.Location = new Point(3, 336);
        processingOutputDirLabel.Name = "processingOutputDirLabel";
        processingOutputDirLabel.Size = new Size(84, 15);
        processingOutputDirLabel.TabIndex = 2;
        processingOutputDirLabel.Text = "Папка вывода";
        // 
        // processingOutputDirTextBox
        // 
        processingOutputDirTextBox.Location = new Point(252, 339);
        processingOutputDirTextBox.Name = "processingOutputDirTextBox";
        processingOutputDirTextBox.Size = new Size(100, 23);
        processingOutputDirTextBox.TabIndex = 3;
        // 
        // processingODataMapPathLabel
        // 
        processingODataMapPathLabel.AutoSize = true;
        processingODataMapPathLabel.Location = new Point(3, 370);
        processingODataMapPathLabel.Name = "processingODataMapPathLabel";
        processingODataMapPathLabel.Size = new Size(74, 15);
        processingODataMapPathLabel.TabIndex = 4;
        processingODataMapPathLabel.Text = "Карта OData";
        // 
        // processingODataMapPathTextBox
        // 
        processingODataMapPathTextBox.Location = new Point(252, 373);
        processingODataMapPathTextBox.Name = "processingODataMapPathTextBox";
        processingODataMapPathTextBox.Size = new Size(100, 23);
        processingODataMapPathTextBox.TabIndex = 5;
        // 
        // oDataTabPage
        // 
        oDataTabPage.Controls.Add(oDataSettingsPanel);
        oDataTabPage.Location = new Point(4, 24);
        oDataTabPage.Name = "oDataTabPage";
        oDataTabPage.Padding = new Padding(3);
        oDataTabPage.Size = new Size(822, 476);
        oDataTabPage.TabIndex = 1;
        oDataTabPage.Text = "OData";
        oDataTabPage.UseVisualStyleBackColor = true;
        // 
        // oDataSettingsPanel
        // 
        oDataSettingsPanel.AutoScroll = true;
        oDataSettingsPanel.Controls.Add(oDataTableLayoutPanel);
        oDataSettingsPanel.Dock = DockStyle.Fill;
        oDataSettingsPanel.Location = new Point(3, 3);
        oDataSettingsPanel.Name = "oDataSettingsPanel";
        oDataSettingsPanel.Padding = new Padding(8);
        oDataSettingsPanel.Size = new Size(816, 470);
        oDataSettingsPanel.TabIndex = 0;
        // 
        // oDataTableLayoutPanel
        // 
        oDataTableLayoutPanel.AutoSize = true;
        oDataTableLayoutPanel.ColumnCount = 2;
        oDataTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 195F));
        oDataTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        oDataTableLayoutPanel.Controls.Add(oDataServiceRootLabel, 0, 0);
        oDataTableLayoutPanel.Controls.Add(oDataServiceRootTextBox, 1, 0);
        oDataTableLayoutPanel.Controls.Add(oDataLoginLabel, 0, 1);
        oDataTableLayoutPanel.Controls.Add(oDataLoginTextBox, 1, 1);
        oDataTableLayoutPanel.Controls.Add(oDataPasswordLabel, 0, 2);
        oDataTableLayoutPanel.Controls.Add(oDataPasswordTextBox, 1, 2);
        oDataTableLayoutPanel.Controls.Add(oDataTimeoutSecondsLabel, 0, 3);
        oDataTableLayoutPanel.Controls.Add(oDataTimeoutSecondsNumericUpDown, 1, 3);
        oDataTableLayoutPanel.Dock = DockStyle.Top;
        oDataTableLayoutPanel.Location = new Point(8, 8);
        oDataTableLayoutPanel.Name = "oDataTableLayoutPanel";
        oDataTableLayoutPanel.Padding = new Padding(0, 4, 16, 8);
        oDataTableLayoutPanel.RowCount = 4;
        oDataTableLayoutPanel.RowStyles.Add(new RowStyle());
        oDataTableLayoutPanel.RowStyles.Add(new RowStyle());
        oDataTableLayoutPanel.RowStyles.Add(new RowStyle());
        oDataTableLayoutPanel.RowStyles.Add(new RowStyle());
        oDataTableLayoutPanel.Size = new Size(800, 128);
        oDataTableLayoutPanel.TabIndex = 0;
        // 
        // oDataServiceRootLabel
        // 
        oDataServiceRootLabel.AutoSize = true;
        oDataServiceRootLabel.Location = new Point(3, 4);
        oDataServiceRootLabel.Name = "oDataServiceRootLabel";
        oDataServiceRootLabel.Size = new Size(87, 15);
        oDataServiceRootLabel.TabIndex = 0;
        oDataServiceRootLabel.Text = "Адрес сервиса";
        // 
        // oDataServiceRootTextBox
        // 
        oDataServiceRootTextBox.Dock = DockStyle.Fill;
        oDataServiceRootTextBox.Location = new Point(198, 7);
        oDataServiceRootTextBox.Name = "oDataServiceRootTextBox";
        oDataServiceRootTextBox.Size = new Size(583, 23);
        oDataServiceRootTextBox.TabIndex = 1;
        // 
        // oDataLoginLabel
        // 
        oDataLoginLabel.AutoSize = true;
        oDataLoginLabel.Location = new Point(3, 33);
        oDataLoginLabel.Name = "oDataLoginLabel";
        oDataLoginLabel.Size = new Size(41, 15);
        oDataLoginLabel.TabIndex = 2;
        oDataLoginLabel.Text = "Логин";
        // 
        // oDataLoginTextBox
        // 
        oDataLoginTextBox.Location = new Point(198, 36);
        oDataLoginTextBox.Name = "oDataLoginTextBox";
        oDataLoginTextBox.Size = new Size(234, 23);
        oDataLoginTextBox.TabIndex = 3;
        // 
        // oDataPasswordLabel
        // 
        oDataPasswordLabel.AutoSize = true;
        oDataPasswordLabel.Location = new Point(3, 62);
        oDataPasswordLabel.Name = "oDataPasswordLabel";
        oDataPasswordLabel.Size = new Size(49, 15);
        oDataPasswordLabel.TabIndex = 4;
        oDataPasswordLabel.Text = "Пароль";
        // 
        // oDataPasswordTextBox
        // 
        oDataPasswordTextBox.Location = new Point(198, 65);
        oDataPasswordTextBox.Name = "oDataPasswordTextBox";
        oDataPasswordTextBox.Size = new Size(234, 23);
        oDataPasswordTextBox.TabIndex = 5;
        // 
        // oDataTimeoutSecondsLabel
        // 
        oDataTimeoutSecondsLabel.AutoSize = true;
        oDataTimeoutSecondsLabel.Location = new Point(3, 91);
        oDataTimeoutSecondsLabel.Name = "oDataTimeoutSecondsLabel";
        oDataTimeoutSecondsLabel.Size = new Size(80, 15);
        oDataTimeoutSecondsLabel.TabIndex = 6;
        oDataTimeoutSecondsLabel.Text = "Таймаут, сек.";
        // 
        // oDataTimeoutSecondsNumericUpDown
        // 
        oDataTimeoutSecondsNumericUpDown.Location = new Point(198, 94);
        oDataTimeoutSecondsNumericUpDown.Name = "oDataTimeoutSecondsNumericUpDown";
        oDataTimeoutSecondsNumericUpDown.Size = new Size(120, 23);
        oDataTimeoutSecondsNumericUpDown.TabIndex = 7;
        // 
        // mailTabPage
        // 
        mailTabPage.Controls.Add(mailSettingsPanel);
        mailTabPage.Location = new Point(4, 24);
        mailTabPage.Name = "mailTabPage";
        mailTabPage.Padding = new Padding(3);
        mailTabPage.Size = new Size(822, 476);
        mailTabPage.TabIndex = 2;
        mailTabPage.Text = "Mail";
        mailTabPage.UseVisualStyleBackColor = true;
        // 
        // mailSettingsPanel
        // 
        mailSettingsPanel.AutoScroll = true;
        mailSettingsPanel.Controls.Add(mailTableLayoutPanel);
        mailSettingsPanel.Dock = DockStyle.Fill;
        mailSettingsPanel.Location = new Point(3, 3);
        mailSettingsPanel.Name = "mailSettingsPanel";
        mailSettingsPanel.Padding = new Padding(8);
        mailSettingsPanel.Size = new Size(816, 470);
        mailSettingsPanel.TabIndex = 0;
        // 
        // mailTableLayoutPanel
        // 
        mailTableLayoutPanel.AutoSize = true;
        mailTableLayoutPanel.ColumnCount = 2;
        mailTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360F));
        mailTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mailTableLayoutPanel.Controls.Add(processingSendEmailCheckBox, 0, 0);
        mailTableLayoutPanel.Controls.Add(mailSmtpHostLabel, 0, 1);
        mailTableLayoutPanel.Controls.Add(mailSmtpHostTextBox, 1, 1);
        mailTableLayoutPanel.Controls.Add(mailSmtpPortLabel, 0, 2);
        mailTableLayoutPanel.Controls.Add(mailSmtpPortNumericUpDown, 1, 2);
        mailTableLayoutPanel.Controls.Add(mailUseStartTlsCheckBox, 0, 3);
        mailTableLayoutPanel.Controls.Add(mailLoginLabel, 0, 4);
        mailTableLayoutPanel.Controls.Add(mailLoginTextBox, 1, 4);
        mailTableLayoutPanel.Controls.Add(mailPasswordLabel, 0, 5);
        mailTableLayoutPanel.Controls.Add(mailPasswordTextBox, 1, 5);
        mailTableLayoutPanel.Controls.Add(mailFromLabel, 0, 6);
        mailTableLayoutPanel.Controls.Add(mailResultEmailLabel, 0, 7);
        mailTableLayoutPanel.Controls.Add(mailResultEmailTextBox, 1, 7);
        mailTableLayoutPanel.Controls.Add(mailFromTextBox, 1, 6);
        mailTableLayoutPanel.Dock = DockStyle.Top;
        mailTableLayoutPanel.Location = new Point(8, 8);
        mailTableLayoutPanel.Name = "mailTableLayoutPanel";
        mailTableLayoutPanel.Padding = new Padding(0, 4, 16, 8);
        mailTableLayoutPanel.RowCount = 8;
        mailTableLayoutPanel.RowStyles.Add(new RowStyle());
        mailTableLayoutPanel.RowStyles.Add(new RowStyle());
        mailTableLayoutPanel.RowStyles.Add(new RowStyle());
        mailTableLayoutPanel.RowStyles.Add(new RowStyle());
        mailTableLayoutPanel.RowStyles.Add(new RowStyle());
        mailTableLayoutPanel.RowStyles.Add(new RowStyle());
        mailTableLayoutPanel.RowStyles.Add(new RowStyle());
        mailTableLayoutPanel.RowStyles.Add(new RowStyle());
        mailTableLayoutPanel.Size = new Size(800, 236);
        mailTableLayoutPanel.TabIndex = 0;
        // 
        // processingSendEmailCheckBox
        // 
        processingSendEmailCheckBox.AutoSize = true;
        mailTableLayoutPanel.SetColumnSpan(processingSendEmailCheckBox, 2);
        processingSendEmailCheckBox.Location = new Point(3, 7);
        processingSendEmailCheckBox.Name = "processingSendEmailCheckBox";
        processingSendEmailCheckBox.Size = new Size(122, 19);
        processingSendEmailCheckBox.TabIndex = 13;
        processingSendEmailCheckBox.Text = "Отправлять email";
        // 
        // mailSmtpHostLabel
        // 
        mailSmtpHostLabel.AutoSize = true;
        mailSmtpHostLabel.Location = new Point(3, 29);
        mailSmtpHostLabel.Name = "mailSmtpHostLabel";
        mailSmtpHostLabel.Size = new Size(81, 15);
        mailSmtpHostLabel.TabIndex = 0;
        mailSmtpHostLabel.Text = "SMTP-сервер";
        // 
        // mailSmtpHostTextBox
        // 
        mailSmtpHostTextBox.Location = new Point(363, 32);
        mailSmtpHostTextBox.Name = "mailSmtpHostTextBox";
        mailSmtpHostTextBox.Size = new Size(100, 23);
        mailSmtpHostTextBox.TabIndex = 1;
        // 
        // mailSmtpPortLabel
        // 
        mailSmtpPortLabel.AutoSize = true;
        mailSmtpPortLabel.Location = new Point(3, 58);
        mailSmtpPortLabel.Name = "mailSmtpPortLabel";
        mailSmtpPortLabel.Size = new Size(69, 15);
        mailSmtpPortLabel.TabIndex = 2;
        mailSmtpPortLabel.Text = "SMTP-порт";
        // 
        // mailSmtpPortNumericUpDown
        // 
        mailSmtpPortNumericUpDown.Location = new Point(363, 61);
        mailSmtpPortNumericUpDown.Name = "mailSmtpPortNumericUpDown";
        mailSmtpPortNumericUpDown.Size = new Size(120, 23);
        mailSmtpPortNumericUpDown.TabIndex = 3;
        // 
        // mailUseStartTlsCheckBox
        // 
        mailUseStartTlsCheckBox.AutoSize = true;
        mailTableLayoutPanel.SetColumnSpan(mailUseStartTlsCheckBox, 2);
        mailUseStartTlsCheckBox.Location = new Point(3, 90);
        mailUseStartTlsCheckBox.Name = "mailUseStartTlsCheckBox";
        mailUseStartTlsCheckBox.Size = new Size(149, 19);
        mailUseStartTlsCheckBox.TabIndex = 5;
        mailUseStartTlsCheckBox.Text = "Использовать StartTLS";
        // 
        // mailLoginLabel
        // 
        mailLoginLabel.AutoSize = true;
        mailLoginLabel.Location = new Point(3, 112);
        mailLoginLabel.Name = "mailLoginLabel";
        mailLoginLabel.Size = new Size(41, 15);
        mailLoginLabel.TabIndex = 6;
        mailLoginLabel.Text = "Логин";
        // 
        // mailLoginTextBox
        // 
        mailLoginTextBox.Location = new Point(363, 115);
        mailLoginTextBox.Name = "mailLoginTextBox";
        mailLoginTextBox.Size = new Size(249, 23);
        mailLoginTextBox.TabIndex = 7;
        // 
        // mailPasswordLabel
        // 
        mailPasswordLabel.AutoSize = true;
        mailPasswordLabel.Location = new Point(3, 141);
        mailPasswordLabel.Name = "mailPasswordLabel";
        mailPasswordLabel.Size = new Size(49, 15);
        mailPasswordLabel.TabIndex = 8;
        mailPasswordLabel.Text = "Пароль";
        // 
        // mailPasswordTextBox
        // 
        mailPasswordTextBox.Location = new Point(363, 144);
        mailPasswordTextBox.Name = "mailPasswordTextBox";
        mailPasswordTextBox.Size = new Size(249, 23);
        mailPasswordTextBox.TabIndex = 9;
        // 
        // mailFromLabel
        // 
        mailFromLabel.AutoSize = true;
        mailFromLabel.Location = new Point(3, 170);
        mailFromLabel.Name = "mailFromLabel";
        mailFromLabel.Size = new Size(78, 15);
        mailFromLabel.TabIndex = 10;
        mailFromLabel.Text = "Отправитель";
        // 
        // mailResultEmailLabel
        // 
        mailResultEmailLabel.AutoSize = true;
        mailResultEmailLabel.Location = new Point(3, 199);
        mailResultEmailLabel.Name = "mailResultEmailLabel";
        mailResultEmailLabel.Size = new Size(103, 15);
        mailResultEmailLabel.TabIndex = 12;
        mailResultEmailLabel.Text = "Email получателя";
        // 
        // mailResultEmailTextBox
        // 
        mailResultEmailTextBox.Location = new Point(363, 202);
        mailResultEmailTextBox.Name = "mailResultEmailTextBox";
        mailResultEmailTextBox.Size = new Size(249, 23);
        mailResultEmailTextBox.TabIndex = 13;
        // 
        // mailFromTextBox
        // 
        mailFromTextBox.Location = new Point(363, 173);
        mailFromTextBox.Name = "mailFromTextBox";
        mailFromTextBox.Size = new Size(249, 23);
        mailFromTextBox.TabIndex = 11;
        // 
        // buttonPanel
        // 
        buttonPanel.AutoSize = true;
        buttonPanel.Controls.Add(closeButton);
        buttonPanel.Controls.Add(runButton);
        buttonPanel.Controls.Add(saveAsButton);
        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Dock = DockStyle.Fill;
        buttonPanel.FlowDirection = FlowDirection.RightToLeft;
        buttonPanel.Location = new Point(15, 581);
        buttonPanel.Name = "buttonPanel";
        buttonPanel.Padding = new Padding(0, 12, 0, 0);
        buttonPanel.Size = new Size(830, 44);
        buttonPanel.TabIndex = 2;
        // 
        // closeButton
        // 
        closeButton.AutoSize = true;
        closeButton.Location = new Point(726, 12);
        closeButton.Margin = new Padding(8, 0, 0, 0);
        closeButton.MinimumSize = new Size(104, 32);
        closeButton.Name = "closeButton";
        closeButton.Size = new Size(104, 32);
        closeButton.TabIndex = 0;
        closeButton.Text = "Закрыть";
        closeButton.UseVisualStyleBackColor = true;
        closeButton.Click += _CloseButton_Click;
        // 
        // runButton
        // 
        runButton.AutoSize = true;
        runButton.Location = new Point(614, 12);
        runButton.Margin = new Padding(8, 0, 0, 0);
        runButton.MinimumSize = new Size(104, 32);
        runButton.Name = "runButton";
        runButton.Size = new Size(104, 32);
        runButton.TabIndex = 1;
        runButton.Text = "Запустить";
        runButton.UseVisualStyleBackColor = true;
        runButton.Click += _RunButton_Click;
        // 
        // saveAsButton
        // 
        saveAsButton.AutoSize = true;
        saveAsButton.Location = new Point(482, 12);
        saveAsButton.Margin = new Padding(8, 0, 0, 0);
        saveAsButton.MinimumSize = new Size(124, 32);
        saveAsButton.Name = "saveAsButton";
        saveAsButton.Size = new Size(124, 32);
        saveAsButton.TabIndex = 2;
        saveAsButton.Text = "Сохранить как...";
        saveAsButton.UseVisualStyleBackColor = true;
        saveAsButton.Click += _SaveAsButton_Click;
        // 
        // saveButton
        // 
        saveButton.AutoSize = true;
        saveButton.Enabled = false;
        saveButton.Location = new Point(370, 12);
        saveButton.Margin = new Padding(8, 0, 0, 0);
        saveButton.MinimumSize = new Size(104, 32);
        saveButton.Name = "saveButton";
        saveButton.Size = new Size(104, 32);
        saveButton.TabIndex = 3;
        saveButton.Text = "Сохранить";
        saveButton.UseVisualStyleBackColor = true;
        saveButton.Click += _SaveButton_Click;
        // 
        // processingStopOnFirstErrorLabel
        // 
        processingStopOnFirstErrorLabel.AutoSize = true;
        processingStopOnFirstErrorLabel.Location = new Point(3, 64);
        processingStopOnFirstErrorLabel.Name = "processingStopOnFirstErrorLabel";
        processingStopOnFirstErrorLabel.Size = new Size(340, 20);
        processingStopOnFirstErrorLabel.TabIndex = 6;
        processingStopOnFirstErrorLabel.Text = "Остановить при первой ошибке";
        // 
        // processingDryRunLabel
        // 
        processingDryRunLabel.AutoSize = true;
        processingDryRunLabel.Location = new Point(3, 84);
        processingDryRunLabel.Name = "processingDryRunLabel";
        processingDryRunLabel.Size = new Size(340, 20);
        processingDryRunLabel.TabIndex = 8;
        processingDryRunLabel.Text = "Пробный запуск";
        // 
        // processingDownloadMetadataOnlyLabel
        // 
        processingDownloadMetadataOnlyLabel.AutoSize = true;
        processingDownloadMetadataOnlyLabel.Location = new Point(3, 104);
        processingDownloadMetadataOnlyLabel.Name = "processingDownloadMetadataOnlyLabel";
        processingDownloadMetadataOnlyLabel.Size = new Size(340, 20);
        processingDownloadMetadataOnlyLabel.TabIndex = 10;
        processingDownloadMetadataOnlyLabel.Text = "Только скачать метаданные";
        // 
        // processingSendEmailLabel
        // 
        processingSendEmailLabel.AutoSize = true;
        processingSendEmailLabel.Location = new Point(3, 124);
        processingSendEmailLabel.Name = "processingSendEmailLabel";
        processingSendEmailLabel.Size = new Size(340, 20);
        processingSendEmailLabel.TabIndex = 12;
        // 
        // processingGeneratePrintFormsLabel
        // 
        processingGeneratePrintFormsLabel.AutoSize = true;
        processingGeneratePrintFormsLabel.Location = new Point(3, 164);
        processingGeneratePrintFormsLabel.Name = "processingGeneratePrintFormsLabel";
        processingGeneratePrintFormsLabel.Size = new Size(340, 20);
        processingGeneratePrintFormsLabel.TabIndex = 16;
        processingGeneratePrintFormsLabel.Text = "Формировать печатные формы";
        // 
        // processingCreateInvoicesOnlyLabel
        // 
        processingCreateInvoicesOnlyLabel.AutoSize = true;
        processingCreateInvoicesOnlyLabel.Location = new Point(3, 184);
        processingCreateInvoicesOnlyLabel.Name = "processingCreateInvoicesOnlyLabel";
        processingCreateInvoicesOnlyLabel.Size = new Size(340, 20);
        processingCreateInvoicesOnlyLabel.TabIndex = 18;
        processingCreateInvoicesOnlyLabel.Text = "Создавать только счета";
        // 
        // processingRealizationOnlyLabel
        // 
        processingRealizationOnlyLabel.AutoSize = true;
        processingRealizationOnlyLabel.Location = new Point(3, 264);
        processingRealizationOnlyLabel.Name = "processingRealizationOnlyLabel";
        processingRealizationOnlyLabel.Size = new Size(340, 20);
        processingRealizationOnlyLabel.TabIndex = 26;
        // 
        // mailUseStartTlsLabel
        // 
        mailUseStartTlsLabel.Location = new Point(3, 44);
        mailUseStartTlsLabel.Name = "mailUseStartTlsLabel";
        mailUseStartTlsLabel.Size = new Size(340, 20);
        mailUseStartTlsLabel.TabIndex = 4;
        // 
        // workingDirectoryPathLabel
        // 
        workingDirectoryPathLabel.AutoSize = true;
        workingDirectoryPathLabel.Dock = DockStyle.Fill;
        workingDirectoryPathLabel.Location = new Point(163, 35);
        workingDirectoryPathLabel.Name = "workingDirectoryPathLabel";
        workingDirectoryPathLabel.Size = new Size(544, 35);
        workingDirectoryPathLabel.TabIndex = 4;
        workingDirectoryPathLabel.Text = "C:\\Users\\chili\\AppData\\Local\\Microsoft\\VisualStudio\\18.0_e0186303\\WinFormsDesigner\\k0aayhfc.dxv";
        workingDirectoryPathLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // SettingsForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(860, 640);
        Controls.Add(mainLayoutPanel);
        MinimumSize = new Size(760, 520);
        Name = "SettingsForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Настройки OneCFreshInvoiceODataBot";
        mainLayoutPanel.ResumeLayout(false);
        mainLayoutPanel.PerformLayout();
        settingsFileLayoutPanel.ResumeLayout(false);
        settingsFileLayoutPanel.PerformLayout();
        settingsTabControl.ResumeLayout(false);
        processingTabPage.ResumeLayout(false);
        processingSettingsPanel.ResumeLayout(false);
        processingSettingsPanel.PerformLayout();
        processingTableLayoutPanel.ResumeLayout(false);
        processingTableLayoutPanel.PerformLayout();
        processingInputXlsxLayoutPanel.ResumeLayout(false);
        processingInputXlsxLayoutPanel.PerformLayout();
        oDataTabPage.ResumeLayout(false);
        oDataSettingsPanel.ResumeLayout(false);
        oDataSettingsPanel.PerformLayout();
        oDataTableLayoutPanel.ResumeLayout(false);
        oDataTableLayoutPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)oDataTimeoutSecondsNumericUpDown).EndInit();
        mailTabPage.ResumeLayout(false);
        mailSettingsPanel.ResumeLayout(false);
        mailSettingsPanel.PerformLayout();
        mailTableLayoutPanel.ResumeLayout(false);
        mailTableLayoutPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)mailSmtpPortNumericUpDown).EndInit();
        buttonPanel.ResumeLayout(false);
        buttonPanel.PerformLayout();
        ResumeLayout(false);
    }

    private static void _ConfigureDesignerLabel(Label label, int width)
    {
        label.AutoSize = true;
        label.Dock = DockStyle.Fill;
        label.Margin = new Padding(0, 5, 12, 5);
        label.Size = new Size(width, 24);
        label.TextAlign = ContentAlignment.MiddleLeft;
    }

    private TextBox processingInnTextBox;
    private Label processingInnLabel;
    private RadioButton rbFromExcel;
    private RadioButton rbFrom1C;
}



