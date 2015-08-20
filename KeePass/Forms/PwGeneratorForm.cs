/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2011 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

using KeePass.App;
using KeePass.UI;
using KeePass.Resources;
using KeePass.Util;

using KeePassLib;
using KeePassLib.Cryptography.PasswordGenerator;
using KeePassLib.Security;
using KeePassLib.Utility;

namespace KeePass.Forms
{
	public partial class PwGeneratorForm : Form
	{
		private const uint MaxPreviewPasswords = 30;

		private PwProfile m_optInitial = null;
		private PwProfile m_optSelected = new PwProfile();

		private readonly string CustomMeta = "(" + KPRes.Custom + ")";
		private readonly string DeriveFromPrevious = "(" + KPRes.GenPwBasedOnPrevious + ")";
		private readonly string AutoGeneratedMeta = "(" + KPRes.AutoGeneratedPasswordSettings + ")";

		private readonly string NoCustomAlgo = "(" + KPRes.None + ")";

		private bool m_bBlockUIUpdate = false;
		private bool m_bCanAccept = true;
		private bool m_bForceInTaskbar = false;

		private string m_strAdvControlText = string.Empty;

		private Dictionary<CustomPwGenerator, string> m_dictCustomOptions =
			new Dictionary<CustomPwGenerator, string>();

		public PwProfile SelectedProfile
		{
			get { return m_optSelected; }
		}

		/// <summary>
		/// Initialize this password generator form instance.
		/// </summary>
		/// <param name="optInitial">Initial options (may be <c>null</c>).</param>
		public void InitEx(PwProfile pwInitial, bool bCanAccept, bool bForceInTaskbar)
		{
			m_optInitial = pwInitial;
			m_bCanAccept = bCanAccept;
			m_bForceInTaskbar = bForceInTaskbar;
		}

		public PwGeneratorForm()
		{
			InitializeComponent();
			Program.Translation.ApplyTo(this);
		}

		private void OnFormLoad(object sender, EventArgs e)
		{
			GlobalWindowManager.AddWindow(this);

			m_strAdvControlText = m_tabAdvanced.Text;

			m_bannerImage.Image = BannerFactory.CreateBanner(m_bannerImage.Width,
				m_bannerImage.Height, BannerStyle.Default,
				Properties.Resources.B48x48_KGPG_Gen, KPRes.PasswordOptions,
				KPRes.PasswordOptionsDesc);
			this.Icon = Properties.Resources.KeePass;

			UIUtil.SetButtonImage(m_btnProfileAdd,
				Properties.Resources.B16x16_FileSaveAs, false);
			UIUtil.SetButtonImage(m_btnProfileRemove,
				Properties.Resources.B16x16_EditDelete, true);

			FontUtil.AssignDefaultBold(m_rbStandardCharSet);
			FontUtil.AssignDefaultBold(m_rbPattern);
			FontUtil.AssignDefaultBold(m_rbCustom);

			m_ttMain.SetToolTip(m_btnProfileAdd, KPRes.GenProfileSaveDesc);
			m_ttMain.SetToolTip(m_btnProfileRemove, KPRes.GenProfileRemoveDesc);

			m_bBlockUIUpdate = true;

			m_cbUpperCase.Text += @" (A, B, C, ...)";
			m_cbLowerCase.Text += @" (a, b, c, ...)";
			m_cbDigits.Text += @" (0, 1, 2, ...)";
			m_cbMinus.Text += @" (-)";
			m_cbUnderline.Text += @" (_)";
			m_cbSpace.Text += @" ( )";
			m_cbSpecial.Text += @" (!, $, %, &&, ...)";
			m_cbBrackets.Text += @" ([, ], {, }, (, ), <, >)";
			m_cbNoRepeat.Text += @" *";
			m_cbExcludeLookAlike.Text += @" (l|1I, O0) *";
			m_lblExcludeChars.Text += @" *";
			m_lblSecRedInfo.Text = @"* " + m_lblSecRedInfo.Text;

			m_cmbCustomAlgo.Items.Add(NoCustomAlgo);
			foreach(CustomPwGenerator pwg in Program.PwGeneratorPool)
			{
				m_cmbCustomAlgo.Items.Add(pwg.Name);
			}
			SelectCustomGenerator((m_optInitial != null) ?
				m_optInitial.CustomAlgorithmUuid : null, null);
			if(m_optInitial != null)
			{
				CustomPwGenerator pwg = GetPwGenerator();
				if(pwg != null) m_dictCustomOptions[pwg] = m_optInitial.CustomAlgorithmOptions;
			}

			m_cmbProfiles.Items.Add(CustomMeta);

			if(m_optInitial != null)
			{
				m_cmbProfiles.Items.Add(DeriveFromPrevious);
				SetGenerationOptions(m_optInitial);
			}

			m_rbStandardCharSet.CheckedChanged += this.UpdateUIProc;
			m_rbPattern.CheckedChanged += this.UpdateUIProc;
			m_rbCustom.CheckedChanged += this.UpdateUIProc;
			m_numGenChars.ValueChanged += this.UpdateUIProc;
			m_cbUpperCase.CheckedChanged += this.UpdateUIProc;
			m_cbLowerCase.CheckedChanged += this.UpdateUIProc;
			m_cbDigits.CheckedChanged += this.UpdateUIProc;
			m_cbMinus.CheckedChanged += this.UpdateUIProc;
			m_cbUnderline.CheckedChanged += this.UpdateUIProc;
			m_cbSpace.CheckedChanged += this.UpdateUIProc;
			m_cbSpecial.CheckedChanged += this.UpdateUIProc;
			m_cbBrackets.CheckedChanged += this.UpdateUIProc;
			m_cbHighAnsi.CheckedChanged += this.UpdateUIProc;
			m_tbCustomChars.TextChanged += this.UpdateUIProc;
			m_tbPattern.TextChanged += this.UpdateUIProc;
			m_cbPatternPermute.CheckedChanged += this.UpdateUIProc;
			m_cbNoRepeat.CheckedChanged += this.UpdateUIProc;
			m_cbExcludeLookAlike.CheckedChanged += this.UpdateUIProc;
			m_tbExcludeChars.TextChanged += this.UpdateUIProc;
			m_cmbCustomAlgo.SelectedIndexChanged += this.UpdateUIProc;

			m_cmbProfiles.Items.Add(AutoGeneratedMeta);

			m_cmbProfiles.SelectedIndex = ((m_optInitial == null) ? 0 : 1);

			PwGeneratorUtil.AddStandardProfilesIfNoneAvailable();

			foreach(PwProfile ppw in Program.Config.PasswordGenerator.UserProfiles)
			{
				m_cmbProfiles.Items.Add(ppw.Name);

				if(ppw.GeneratorType == PasswordGeneratorType.Custom)
				{
					CustomPwGenerator pwg = Program.PwGeneratorPool.Find(new
						PwUuid(Convert.FromBase64String(ppw.CustomAlgorithmUuid)));
					if(pwg != null) m_dictCustomOptions[pwg] = ppw.CustomAlgorithmOptions;
				}
			}
	
			if(m_optInitial == null)
			{
				// int nIndex = m_cmbProfiles.FindString(Program.Config.PasswordGenerator.LastUsedProfile.Name);
				// if(nIndex >= 0) m_cmbProfiles.SelectedIndex = nIndex;
				SetGenerationOptions(Program.Config.PasswordGenerator.LastUsedProfile);
			}

			if(m_bCanAccept == false)
			{
				m_btnOK.Visible = false;
				m_btnCancel.Text = KPRes.CloseButton;

				m_tabPreview.Text = KPRes.Generate;
				m_lblPreview.Visible = false;
				UIUtil.SetChecked(m_cbEntropy, false);
				m_cbEntropy.Enabled = false;
			}

			Debug.Assert(this.ShowInTaskbar == false);
			if(m_bForceInTaskbar) this.ShowInTaskbar = true;

			CustomizeForScreenReader();

			m_bBlockUIUpdate = false;
			EnableControlsEx(false);
		}

		private void CustomizeForScreenReader()
		{
			if(!Program.Config.UI.OptimizeForScreenReader) return;

			m_btnProfileAdd.Text = KPRes.GenProfileSave;
			m_btnProfileRemove.Text = KPRes.GenProfileRemove;
			m_btnCustomOpt.Text = KPRes.Options;
		}

		private void EnableControlsEx(bool bSwitchToCustomProfile)
		{
			if(m_bBlockUIUpdate) return;

			m_bBlockUIUpdate = true;

			if(bSwitchToCustomProfile)
				m_cmbProfiles.SelectedIndex = 0;

			m_lblNumGenChars.Enabled = m_numGenChars.Enabled = m_cbUpperCase.Enabled =
				m_cbLowerCase.Enabled = m_cbDigits.Enabled = m_cbMinus.Enabled =
				m_cbUnderline.Enabled = m_cbSpace.Enabled = m_cbSpecial.Enabled =
				m_cbBrackets.Enabled = m_cbHighAnsi.Enabled = m_lblCustomChars.Enabled =
				m_tbCustomChars.Enabled = m_rbStandardCharSet.Checked;
			m_tbPattern.Enabled = m_cbPatternPermute.Enabled =
				m_rbPattern.Checked;

			string strProfile = m_cmbProfiles.Text;
			m_btnProfileRemove.Enabled = ((strProfile != CustomMeta) &&
				(strProfile != DeriveFromPrevious) &&
				(strProfile != AutoGeneratedMeta));

			m_tabAdvanced.Text = ((m_cbExcludeLookAlike.Checked ||
				m_cbNoRepeat.Checked || (m_tbExcludeChars.Text.Length > 0)) ?
				(m_strAdvControlText + " (!)") : m_strAdvControlText);

			m_cmbCustomAlgo.Enabled = m_rbCustom.Checked;
			if(m_rbCustom.Checked == false) m_btnCustomOpt.Enabled = false;
			else
			{
				CustomPwGenerator pwg = GetPwGenerator();
				if(pwg != null) m_btnCustomOpt.Enabled = pwg.SupportsOptions;
				else m_btnCustomOpt.Enabled = false;
			}

			m_bBlockUIUpdate = false;
		}

		private void CleanUpEx()
		{
			Program.Config.PasswordGenerator.LastUsedProfile = GetGenerationOptions();

			if(m_bForceInTaskbar) this.ShowInTaskbar = false;
		}

		private void OnBtnOK(object sender, EventArgs e)
		{
			m_optSelected = GetGenerationOptions();
		}

		private void OnBtnCancel(object sender, EventArgs e)
		{
		}

		private PwProfile GetGenerationOptions()
		{
			PwProfile opt = new PwProfile();

			opt.Name = m_cmbProfiles.Text;

			if(m_rbStandardCharSet.Checked)
				opt.GeneratorType = PasswordGeneratorType.CharSet;
			else if(m_rbPattern.Checked)
				opt.GeneratorType = PasswordGeneratorType.Pattern;
			else if(m_rbCustom.Checked)
				opt.GeneratorType = PasswordGeneratorType.Custom;

			opt.Length = (uint)m_numGenChars.Value;

			opt.CharSet = new PwCharSet();

			if(m_cbUpperCase.Checked) opt.CharSet.Add(PwCharSet.UpperCase);
			if(m_cbLowerCase.Checked) opt.CharSet.Add(PwCharSet.LowerCase);
			if(m_cbDigits.Checked) opt.CharSet.Add(PwCharSet.Digits);
			if(m_cbSpecial.Checked) opt.CharSet.Add(opt.CharSet.SpecialChars);
			if(m_cbHighAnsi.Checked) opt.CharSet.Add(opt.CharSet.HighAnsiChars);
			if(m_cbMinus.Checked) opt.CharSet.Add('-');
			if(m_cbUnderline.Checked) opt.CharSet.Add('_');
			if(m_cbSpace.Checked) opt.CharSet.Add(' ');
			if(m_cbBrackets.Checked) opt.CharSet.Add(PwCharSet.Brackets);

			opt.CharSet.Add(m_tbCustomChars.Text);

			opt.Pattern = m_tbPattern.Text;
			opt.PatternPermutePassword = m_cbPatternPermute.Checked;

			opt.CollectUserEntropy = m_cbEntropy.Checked;
			opt.ExcludeLookAlike = m_cbExcludeLookAlike.Checked;
			opt.NoRepeatingCharacters = m_cbNoRepeat.Checked;
			opt.ExcludeCharacters = m_tbExcludeChars.Text;

			CustomPwGenerator pwg = GetPwGenerator();
			opt.CustomAlgorithmUuid = ((pwg != null) ? Convert.ToBase64String(
				pwg.Uuid.UuidBytes) : string.Empty);
			if((pwg != null) && m_dictCustomOptions.ContainsKey(pwg))
				opt.CustomAlgorithmOptions = (m_dictCustomOptions[pwg] ?? string.Empty);
			else opt.CustomAlgorithmOptions = string.Empty;

			return opt;
		}

		private void SetGenerationOptions(PwProfile opt)
		{
			bool bPrevInit = m_bBlockUIUpdate;
			m_bBlockUIUpdate = true;

			m_rbStandardCharSet.Checked = (opt.GeneratorType == PasswordGeneratorType.CharSet);
			m_rbPattern.Checked = (opt.GeneratorType == PasswordGeneratorType.Pattern);
			m_rbCustom.Checked = (opt.GeneratorType == PasswordGeneratorType.Custom);

			m_numGenChars.Value = opt.Length;

			PwCharSet pcs = new PwCharSet(opt.CharSet.ToString());

			m_cbUpperCase.Checked = pcs.RemoveIfAllExist(PwCharSet.UpperCase);
			m_cbLowerCase.Checked = pcs.RemoveIfAllExist(PwCharSet.LowerCase);
			m_cbDigits.Checked = pcs.RemoveIfAllExist(PwCharSet.Digits);
			m_cbSpecial.Checked = pcs.RemoveIfAllExist(pcs.SpecialChars);
			m_cbHighAnsi.Checked = pcs.RemoveIfAllExist(pcs.HighAnsiChars);
			m_cbMinus.Checked = pcs.RemoveIfAllExist("-");
			m_cbUnderline.Checked = pcs.RemoveIfAllExist("_");
			m_cbSpace.Checked = pcs.RemoveIfAllExist(" ");
			m_cbBrackets.Checked = pcs.RemoveIfAllExist(PwCharSet.Brackets);

			m_tbCustomChars.Text = pcs.ToString();

			m_tbPattern.Text = opt.Pattern;
			m_cbPatternPermute.Checked = opt.PatternPermutePassword;

			m_cbEntropy.Checked = opt.CollectUserEntropy;
			m_cbExcludeLookAlike.Checked = opt.ExcludeLookAlike;
			m_cbNoRepeat.Checked = opt.NoRepeatingCharacters;
			m_tbExcludeChars.Text = opt.ExcludeCharacters;

			SelectCustomGenerator(opt.CustomAlgorithmUuid, opt.CustomAlgorithmOptions);

			m_bBlockUIUpdate = bPrevInit;
		}

		private void UpdateUIProc(object sender, EventArgs e)
		{
			EnableControlsEx(true);
		}

		private void OnProfilesSelectedIndexChanged(object sender, EventArgs e)
		{
			if(m_bBlockUIUpdate) return;

			string strProfile = m_cmbProfiles.Text;

			if(strProfile == CustomMeta) { } // Switch to custom -> nothing to do
			else if(strProfile == DeriveFromPrevious)
				SetGenerationOptions(m_optInitial);
			else if(strProfile == AutoGeneratedMeta)
				SetGenerationOptions(Program.Config.PasswordGenerator.AutoGeneratedPasswordsProfile);
			else
			{
				foreach(PwProfile pwgo in Program.Config.PasswordGenerator.UserProfiles)
				{
					if(pwgo.Name == strProfile)
					{
						SetGenerationOptions(pwgo);
						break;
					}
				}
			}

			EnableControlsEx(false);
		}

		private void OnBtnProfileSave(object sender, EventArgs e)
		{
			List<string> lNames = new List<string>();
			lNames.Add(AutoGeneratedMeta);
			foreach(PwProfile pwExisting in Program.Config.PasswordGenerator.UserProfiles)
				lNames.Add(pwExisting.Name);

			SingleLineEditForm slef = new SingleLineEditForm();
			slef.InitEx(KPRes.GenProfileSave, KPRes.GenProfileSaveDesc,
				KPRes.GenProfileSaveDescLong, Properties.Resources.B48x48_KGPG_Gen,
				string.Empty, lNames.ToArray());

			if(slef.ShowDialog() == DialogResult.OK)
			{
				string strProfile = slef.ResultString;

				PwProfile pwCurrent = GetGenerationOptions();
				pwCurrent.Name = strProfile;

				if(strProfile.Equals(CustomMeta) || strProfile.Equals(DeriveFromPrevious) ||
					(strProfile.Length == 0))
				{
					MessageService.ShowWarning(KPRes.FieldNameInvalid);
				}
				else if(strProfile == AutoGeneratedMeta)
				{
					pwCurrent.Name = string.Empty;
					Program.Config.PasswordGenerator.AutoGeneratedPasswordsProfile = pwCurrent;
					m_cmbProfiles.SelectedIndex = m_cmbProfiles.FindString(AutoGeneratedMeta);
				}
				else
				{
					bool bExists = false;
					for(int i = 0; i < Program.Config.PasswordGenerator.UserProfiles.Count; ++i)
					{
						if(Program.Config.PasswordGenerator.UserProfiles[i].Name == strProfile)
						{
							Program.Config.PasswordGenerator.UserProfiles[i] = pwCurrent;
							m_cmbProfiles.SelectedIndex = m_cmbProfiles.FindString(strProfile);
							bExists = true;
						}
					}

					if(bExists == false)
					{
						Program.Config.PasswordGenerator.UserProfiles.Add(pwCurrent);
						m_cmbProfiles.Items.Add(strProfile);
						m_cmbProfiles.SelectedIndex = m_cmbProfiles.Items.Count - 1;
					}
				}
			}

			EnableControlsEx(false);
		}

		private void OnBtnProfileRemove(object sender, EventArgs e)
		{
			string strProfile = m_cmbProfiles.Text;

			if((strProfile == CustomMeta) || (strProfile == DeriveFromPrevious) ||
				(strProfile == AutoGeneratedMeta))
				return;

			m_cmbProfiles.SelectedIndex = 0;
			for(int i = 0; i < m_cmbProfiles.Items.Count; ++i)
			{
				if(strProfile == m_cmbProfiles.Items[i].ToString())
				{
					m_cmbProfiles.Items.RemoveAt(i);

					List<PwProfile> lProfiles = Program.Config.PasswordGenerator.UserProfiles;
					for(int j = 0; j < lProfiles.Count; ++j)
					{
						if(lProfiles[j].Name == strProfile)
						{
							lProfiles.RemoveAt(j);
							break;
						}
					}

					break;
				}
			}
		}

		private void OnBtnHelp(object sender, EventArgs e)
		{
			AppHelp.ShowHelp(AppDefs.HelpTopics.PwGenerator, null);
		}

		private void OnTabMainSelectedIndexChanged(object sender, EventArgs e)
		{
			if(m_bBlockUIUpdate) return;

			if(m_tabMain.SelectedTab == m_tabPreview)
				GeneratePreviewPasswords();
		}

		private void GeneratePreviewPasswords()
		{
			m_pbPreview.Value = 0;
			m_tbPreview.Text = string.Empty;

			PwProfile pwOpt = GetGenerationOptions();
			StringBuilder sbList = new StringBuilder();

			Cursor cNormalCursor = this.Cursor;
			this.Cursor = Cursors.WaitCursor;

			for(uint i = 0; i < MaxPreviewPasswords; ++i)
			{
				Application.DoEvents();

				ProtectedString psNew = new ProtectedString(false);
				PwGenerator.Generate(psNew, pwOpt, null, Program.PwGeneratorPool);
				sbList.AppendLine(psNew.ReadString());
				m_pbPreview.Value = (int)((100 * i) / MaxPreviewPasswords);
			}
			
			m_pbPreview.Value = 100;
			m_tbPreview.Text = sbList.ToString();

			this.Cursor = cNormalCursor;
		}

		private CustomPwGenerator GetPwGenerator()
		{
			string strAlgo = (m_cmbCustomAlgo.SelectedItem as string);
			if(strAlgo == null) return null;

			return Program.PwGeneratorPool.Find(strAlgo);
		}

		private void SelectCustomGenerator(string strUuid, string strCustomOptions)
		{
			try
			{
				if(string.IsNullOrEmpty(strUuid)) throw new ArgumentException();

				PwUuid uuid = new PwUuid(Convert.FromBase64String(strUuid));
				CustomPwGenerator pwg = Program.PwGeneratorPool.Find(uuid);
				if(pwg == null) throw new ArgumentException();

				bool bSet = false;
				for(int i = 0; i < m_cmbCustomAlgo.Items.Count; ++i)
				{
					if((m_cmbCustomAlgo.Items[i] as string) == pwg.Name)
					{
						m_cmbCustomAlgo.SelectedIndex = i;

						if(strCustomOptions != null)
							m_dictCustomOptions[pwg] = strCustomOptions;

						bSet = true;
						break;
					}
				}

				if(!bSet) throw new ArgumentException();
			}
			catch(Exception) { m_cmbCustomAlgo.SelectedIndex = 0; }
		}

		private void OnFormClosed(object sender, FormClosedEventArgs e)
		{
			GlobalWindowManager.RemoveWindow(this);
		}

		private void OnBtnCustomOpt(object sender, EventArgs e)
		{
			CustomPwGenerator pwg = GetPwGenerator();
			if(pwg == null) { Debug.Assert(false); return; }
			if(!pwg.SupportsOptions) { Debug.Assert(false); return; }

			string strCurOpt = string.Empty;
			if(m_dictCustomOptions.ContainsKey(pwg))
				strCurOpt = (m_dictCustomOptions[pwg] ?? string.Empty);

			m_dictCustomOptions[pwg] = pwg.GetOptions(strCurOpt);
		}

		private void OnFormClosing(object sender, FormClosingEventArgs e)
		{
			CleanUpEx();
		}
	}
}
