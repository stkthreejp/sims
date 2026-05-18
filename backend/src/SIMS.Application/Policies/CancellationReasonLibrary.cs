using System.Text.RegularExpressions;

namespace SIMS.Application.Policies;

public sealed record CancellationReason(
    string Code,
    string Category,
    string Label,
    int DefaultNoticeRequirementDays,
    string NoticeRequirementLabel,
    string LanguageTemplate,
    bool RequiresSpecialHandling = false)
{
    private static readonly Regex TokenRegex = new(@"\[([A-Z0-9_]+)\]", RegexOptions.Compiled);

    public IReadOnlyList<string> RequiredInputTokens { get; } = TokenRegex
        .Matches(LanguageTemplate)
        .Select(m => m.Groups[1].Value)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public string Resolve(IReadOnlyDictionary<string, string> inputs)
    {
        var missing = RequiredInputTokens
            .Where(token => !inputs.TryGetValue(token, out var value) || string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (missing.Length > 0)
            throw new InvalidOperationException($"Missing cancellation reason input(s): {string.Join(", ", missing)}.");

        return TokenRegex.Replace(LanguageTemplate, match =>
        {
            var token = match.Groups[1].Value;
            return inputs[token].Trim();
        });
    }
}

public static class CancellationReasonLibrary
{
    public static IReadOnlyList<CancellationReason> All { get; } =
    [
        new(
            "NP-01",
            "Non-Payment of Premium",
            "Non-Payment - Standard",
            10,
            "10 days",
            "Non-payment of premium. The amount currently past due is $[AMOUNT_DUE]. If full payment is received prior to the effective date of cancellation, this notice will be rescinded and coverage will remain in force without interruption."),
        new(
            "NP-02",
            "Non-Payment of Premium",
            "Non-Payment - Premium Finance",
            10,
            "10 days",
            "Non-payment of premium pursuant to a premium finance agreement. Notice of cancellation has been issued at the direction of [FINANCE_COMPANY], the premium finance company of record, due to default under the terms of the finance agreement. Any return premium will be remitted directly to [FINANCE_COMPANY] to be applied against any outstanding balance."),
        new(
            "UW-01",
            "New Policy / Underwriting Period",
            "Underwriting Review - General (Free Look)",
            20,
            "20 days",
            "Upon underwriting review conducted during the initial policy period, it has been determined that this risk does not meet the insurer's underwriting standards. This cancellation is being issued pursuant to the insurer's right to cancel during the underwriting review period."),
        new(
            "UW-02",
            "New Policy / Underwriting Period",
            "Inspection Finding - Unacceptable Conditions",
            20,
            "20 days",
            "Upon physical inspection of the insured premises/operations conducted after policy issuance, conditions were identified that do not meet the insurer's objective, uniformly applied underwriting standards. Specifically: [DESCRIBE_CONDITIONS]. This cancellation is being issued pursuant to the insurer's right to cancel during the underwriting review period."),
        new(
            "UW-03",
            "New Policy / Underwriting Period",
            "Information Discovered Post-Bind",
            20,
            "20 days",
            "Underwriting information obtained after policy issuance reveals that the risk does not qualify for coverage under applicable underwriting guidelines. Specifically: [DESCRIBE_INFORMATION]. This cancellation is being issued pursuant to the insurer's right to cancel during the underwriting review period."),
        new(
            "FR-01",
            "Fraud and Material Misrepresentation",
            "Material Misrepresentation - Application",
            30,
            "15-30 days",
            "Discovery of material misrepresentation in the obtaining of the policy. Specifically, information provided in the application or submission materials has been found to be materially false, incomplete, or misleading in a manner that would have affected the insurer's decision to issue coverage or the terms under which coverage was offered. Specifically: [DESCRIBE_MISREPRESENTATION]."),
        new(
            "FR-02",
            "Fraud and Material Misrepresentation",
            "Fraud or Misrepresentation - Claim",
            30,
            "15-30 days",
            "Discovery of fraud or material misrepresentation in the presentation of a claim under the policy. Specifically: [DESCRIBE_CIRCUMSTANCES]."),
        new(
            "FR-03",
            "Fraud and Material Misrepresentation",
            "Concealment of Material Fact",
            30,
            "15-30 days",
            "Discovery of the concealment of a material fact at the time of application that, had it been disclosed, would have affected the insurer's underwriting decision. Specifically: [DESCRIBE_CONCEALED_FACT]."),
        new(
            "IH-01",
            "Substantial Increase in Hazard",
            "Change in Operations - Increased Hazard",
            30,
            "30 days",
            "Discovery, after issuance of the policy, of a change in the named insured's operations or activities that substantially and materially increases the hazard insured against and which occurred subsequent to inception of the current policy period. Specifically: [DESCRIBE_CHANGE_IN_OPERATIONS]."),
        new(
            "IH-02",
            "Substantial Increase in Hazard",
            "Unreported Drivers / Vehicles (Commercial Auto)",
            30,
            "30 days",
            "Discovery, after issuance of the policy, of unreported drivers or vehicles operating under the insured's commercial auto program, including drivers with major violations or loss history not disclosed at application, substantially and materially increasing the hazard insured against. Specifically: [DESCRIBE_DRIVERS_OR_VEHICLES]."),
        new(
            "IH-03",
            "Substantial Increase in Hazard",
            "Willful or Reckless Acts",
            30,
            "30 days",
            "Discovery of willful or reckless acts or omissions by the named insured or their representatives that substantially and materially increase the hazard insured against. Specifically: [DESCRIBE_ACTS_OR_OMISSIONS]."),
        new(
            "IH-04",
            "Substantial Increase in Hazard",
            "Violation of Safety Standards / Regulations",
            30,
            "30 days",
            "Discovery of violations of applicable state laws, regulations, or safety standards by the named insured that materially increase the hazard insured against. Specifically: [DESCRIBE_VIOLATIONS]."),
        new(
            "IH-05",
            "Substantial Increase in Hazard",
            "Failure to Comply with Loss Control Requirements",
            30,
            "30 days",
            "Failure by the named insured to implement loss control measures or safety recommendations that were a condition of policy issuance or a condition of the applicable rating plan. Specifically: [DESCRIBE_REQUIREMENTS_AND_FAILURE_TO_COMPLY]. This failure materially increases the hazard insured against."),
        new(
            "PC-01",
            "Physical or Property Changes",
            "Material Physical Change - Property Uninsurable",
            30,
            "30 days",
            "Material physical change in the insured property occurring after issuance of the policy that results in the property becoming uninsurable in accordance with the insurer's objective, uniformly applied underwriting standards in effect at the time the policy was issued or last renewed. Specifically: [DESCRIBE_PHYSICAL_CHANGE]."),
        new(
            "PC-02",
            "Physical or Property Changes",
            "Material Change in Nature or Extent of Risk",
            30,
            "30 days",
            "Material change in the nature or extent of the risk, occurring after issuance of the policy, that causes the risk to be outside the scope of coverage for which the policy was issued. Specifically: [DESCRIBE_CHANGE_IN_RISK]."),
        new(
            "LR-01",
            "Legal, Regulatory, and License-Related",
            "License Suspension / Revocation",
            30,
            "30 days",
            "Suspension or revocation of a license, permit, or certification required by applicable law for the named insured to conduct the operations covered under this policy. Specifically: [DESCRIBE_LICENSE_ACTION]."),
        new(
            "LR-02",
            "Legal, Regulatory, and License-Related",
            "Criminal Conviction - Increases Hazard",
            30,
            "30 days",
            "Conviction of the named insured of a crime arising out of acts that increase the hazard insured against under this policy. Specifically: [DESCRIBE_CONVICTION]."),
        new(
            "LR-03",
            "Legal, Regulatory, and License-Related",
            "Continuation Would Violate Law / Regulatory Order",
            30,
            "30 days",
            "A determination that continuation of this policy would place the insurer in violation of the laws of this state or the state of its domicile, or that continuation of coverage would threaten the solvency of the insurer, as determined by the applicable Insurance Commissioner."),
        new(
            "LR-04",
            "Legal, Regulatory, and License-Related",
            "Court / Administrative Tribunal Judgment",
            30,
            "30 days",
            "A judgment by a court or administrative tribunal that the named insured has violated a law having as one of its necessary elements an act that materially increases the risks insured against under this policy. Specifically: [DESCRIBE_JUDGMENT]."),
        new(
            "AR-01",
            "Arson and Intentional Loss Risk",
            "Arson / Intentional Destruction Risk",
            15,
            "15 days",
            "A determination that there exists a risk or danger that the insured will destroy, or permit to be destroyed, the insured property for the purpose of collecting insurance proceeds. NOTE: This reason code requires specific procedural compliance including simultaneous notice to the applicable regulatory authority. Consult legal counsel and applicable state law before use. Specifically: [DESCRIBE_BASIS_FOR_DETERMINATION].",
            RequiresSpecialHandling: true),
        new(
            "RE-01",
            "Reinsurance / Insurer Solvency",
            "Loss or Change in Reinsurance",
            30,
            "30 days",
            "Loss of, or material change in, the insurer's reinsurance covering all or part of the risk covered by this policy, which threatens the financial integrity or solvency of the insurer if cancellation is not permitted, as certified to the applicable Insurance Commissioner pursuant to applicable statutory requirements.")
    ];

    public static CancellationReason? GetByCode(string code)
        => All.FirstOrDefault(r => string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase));
}
