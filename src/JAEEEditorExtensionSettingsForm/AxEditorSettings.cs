using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace JAEE.AX.EditorExtensions
{
    public partial class AxEditorSettings : Form
    {
        private EditorSettings singletonSettings = null;

        #region - SETTINGS -

        private void loadSettings()
        {
            singletonSettings = EditorSettings.getInstance();

            HighlightWordProperties highlightWordProperties = new HighlightWordProperties(singletonSettings.HighlightWord);
            this.propHighlightWord.SelectedObject = highlightWordProperties;

            HighlightLineProperties highlightLineProperties = new HighlightLineProperties(singletonSettings.HighlightCurrentLine);
            this.propHighlightLine.SelectedObject = highlightLineProperties;

            SyntaxHighlighterProperties syntaxHighlighterProperties = new SyntaxHighlighterProperties(singletonSettings.SyntaxHighlighter);
            this.propSyntaxHighlighter.SelectedObject = syntaxHighlighterProperties;

            nRows.Value = singletonSettings.Outlining.MaxRowsInTooltip;
        }

        private void saveSettings()
        {
            HighlightWordProperties propHighlightWord = (HighlightWordProperties)this.propHighlightWord.SelectedObject;
            HighlightLineProperties propHighlightLine = (HighlightLineProperties)this.propHighlightLine.SelectedObject;
            SyntaxHighlighterProperties propSyntaxHighlighter = (SyntaxHighlighterProperties)this.propSyntaxHighlighter.SelectedObject;

            singletonSettings = EditorSettings.getInstance();

            // Highliht selected word
            singletonSettings.HighlightWord.BackColor = propHighlightWord.BackColor;
            singletonSettings.HighlightWord.FrameColor = propHighlightWord.FrameColor;

            // Highlight current line
            singletonSettings.HighlightCurrentLine.BackColor = propHighlightLine.BackColor;
            singletonSettings.HighlightCurrentLine.FrameColor = propHighlightLine.FrameColor;
            singletonSettings.HighlightCurrentLine.BackOpacity = propHighlightLine.BackOpacity;

            // Outlining
            singletonSettings.Outlining.MaxRowsInTooltip = Convert.ToInt32(nRows.Value);

            // Syntax highlighter
            singletonSettings.SyntaxHighlighter.TypeEnabled   = propSyntaxHighlighter.TypeEnabled;
            singletonSettings.SyntaxHighlighter.TypeColor     = propSyntaxHighlighter.TypeColor;
            singletonSettings.SyntaxHighlighter.MacroEnabled  = propSyntaxHighlighter.MacroEnabled;
            singletonSettings.SyntaxHighlighter.MacroColor    = propSyntaxHighlighter.MacroColor;
            singletonSettings.SyntaxHighlighter.MethodEnabled = propSyntaxHighlighter.MethodEnabled;
            singletonSettings.SyntaxHighlighter.MethodColor   = propSyntaxHighlighter.MethodColor;
            singletonSettings.SyntaxHighlighter.MethodGlobalEnabled = propSyntaxHighlighter.MethodGlobalEnabled;
            singletonSettings.SyntaxHighlighter.MethodGlobalColor   = propSyntaxHighlighter.MethodGlobalColor;
            singletonSettings.SyntaxHighlighter.ParameterEnabled    = propSyntaxHighlighter.ParameterEnabled;
            singletonSettings.SyntaxHighlighter.ParameterColor      = propSyntaxHighlighter.ParameterColor;
            singletonSettings.SyntaxHighlighter.LocalEnabled        = propSyntaxHighlighter.LocalEnabled;
            singletonSettings.SyntaxHighlighter.LocalColor          = propSyntaxHighlighter.LocalColor;

            singletonSettings.saveChanges();
        }

        #endregion

        #region - FORM -
        
        public AxEditorSettings()
        {
            InitializeComponent();
        }

        private void AxEditorSettings_Load(object sender, EventArgs e)
        {
            this.loadSettings();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            this.saveSettings();
            MessageBox.Show("Please reopen Microsot Dynamics AX client to see the changes.");
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // Repopulates every tab with fresh default values. Nothing is persisted until
        // the user clicks OK (Cancel discards the reset).
        private void btnDefaults_Click(object sender, EventArgs e)
        {
            this.propHighlightWord.SelectedObject = new HighlightWordProperties(new JAEEHighlightWordSettings());
            this.propHighlightLine.SelectedObject = new HighlightLineProperties(new JAEECurrentLineHighlightSettings());
            this.propSyntaxHighlighter.SelectedObject = new SyntaxHighlighterProperties(new JAEESyntaxHighlighterSettings());

            decimal rows = new JAEEOutliningSettings().MaxRowsInTooltip;
            if (rows < this.nRows.Minimum) rows = this.nRows.Minimum;
            if (rows > this.nRows.Maximum) rows = this.nRows.Maximum;
            this.nRows.Value = rows;

            this.propHighlightWord.Refresh();
            this.propHighlightLine.Refresh();
            this.propSyntaxHighlighter.Refresh();
        }

        #endregion

    }
}
