import { useEffect, useMemo, useState } from "react";
import { useForm, type UseFormRegisterReturn } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import clsx from "clsx";
import {
  MemberRole,
  calculateAge,
  isMinor,
  registerMemberSchema,
  type RegisterMemberInput,
  type RegisterMemberResponse,
} from "./schema";
import {
  ApiError,
  ApiValidationError,
  registerMember,
} from "../../services/membersApi";

const DEFAULTS: RegisterMemberInput = {
  fullName: "",
  dateOfBirth: "",
  role: MemberRole.Athlete,
  guardianConsent: false,
  email: "",
  phoneNumber: "",
  nationalId: "",
  acceptTerms: false,
  acceptPrivacyPolicy: false,
  acceptCodeOfConduct: false,
  guardianFullName: "",
  guardianEmail: "",
  guardianPhone: "",
  guardianRelation: undefined,
  guardianNationalId: "",
};

const RELATIONS = ["Mother", "Father", "LegalGuardian", "Grandparent", "Sibling", "Other"] as const;

const FIELD_LABELS: Record<keyof RegisterMemberInput, string> = {
  fullName: "Full name",
  dateOfBirth: "Date of birth",
  role: "Role",
  guardianConsent: "Guardian consent",
  email: "Email",
  phoneNumber: "Phone number",
  nationalId: "National ID",
  acceptTerms: "Terms of Service checkbox",
  acceptPrivacyPolicy: "Privacy Policy checkbox",
  acceptCodeOfConduct: "Code of Conduct checkbox",
  guardianFullName: "Guardian full name",
  guardianEmail: "Guardian email",
  guardianPhone: "Guardian phone",
  guardianRelation: "Guardian relation",
  guardianNationalId: "Guardian national ID",
};

export function RegisterMemberForm() {
  const {
    register,
    handleSubmit,
    watch,
    setValue,
    setError,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<RegisterMemberInput>({
    resolver: zodResolver(registerMemberSchema),
    mode: "onBlur",
    defaultValues: DEFAULTS,
  });

  const role = watch("role");
  const dateOfBirth = watch("dateOfBirth");

  const age = useMemo(
    () => (dateOfBirth ? calculateAge(dateOfBirth) : NaN),
    [dateOfBirth],
  );

  const showGuardianConsent =
    role === MemberRole.Athlete &&
    !!dateOfBirth &&
    Number.isFinite(age) &&
    isMinor(dateOfBirth);

  useEffect(() => {
    if (!showGuardianConsent) {
      setValue("guardianConsent", false, { shouldValidate: false, shouldDirty: false });
      setValue("guardianFullName", "", { shouldValidate: false, shouldDirty: false });
      setValue("guardianEmail", "", { shouldValidate: false, shouldDirty: false });
      setValue("guardianPhone", "", { shouldValidate: false, shouldDirty: false });
      setValue("guardianRelation", undefined, { shouldValidate: false, shouldDirty: false });
      setValue("guardianNationalId", "", { shouldValidate: false, shouldDirty: false });
    }
  }, [showGuardianConsent, setValue]);

  const [result, setResult] = useState<RegisterMemberResponse | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [errorList, setErrorList] = useState<Array<{ field: string; message: string }>>([]);

  const onInvalid = (formErrors: typeof errors) => {
    setResult(null);
    const list = Object.entries(formErrors).map(([key, err]) => ({
      field: FIELD_LABELS[key as keyof RegisterMemberInput] ?? key,
      message:
        (err as { message?: string } | undefined)?.message ?? "This field is invalid.",
    }));
    setErrorList(list);
    setSubmitError(
      list.length > 0
        ? `Please fix the following before registering:`
        : "Please complete the required fields below.",
    );
    // Jump to the first field with an error so it's never hidden off-screen.
    const firstKey = Object.keys(formErrors)[0];
    if (firstKey) {
      const el = document.getElementById(firstKey);
      if (el) {
        el.scrollIntoView({ behavior: "smooth", block: "center" });
        (el as HTMLElement).focus?.();
      }
    }
  };

  const onSubmit = handleSubmit(async (data) => {
    setSubmitError(null);
    setErrorList([]);
    setResult(null);

    try {
      const response = await registerMember(data);
      setResult(response);
      reset(DEFAULTS);
    } catch (err) {
      if (err instanceof ApiValidationError) {
        for (const [field, messages] of Object.entries(err.errors)) {
          if (!messages.length) continue;
          setError(field as keyof RegisterMemberInput, {
            type: "server",
            message: messages[0],
          });
        }
        setSubmitError(err.message);
        return;
      }

      if (err instanceof ApiError) {
        setSubmitError(
          err.status
            ? `Request failed (${err.status}): ${err.message}`
            : err.message,
        );
        return;
      }

      setSubmitError("Something went wrong. Please try again.");
    }
  }, onInvalid);

  const todayIso = new Date().toISOString().slice(0, 10);

  return (
    <form
      onSubmit={onSubmit}
      noValidate
      className="space-y-5"
      aria-describedby={submitError ? "form-error" : undefined}
    >
      {submitError && (
        <div
          id="form-error"
          role="alert"
          className="rounded-lg border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-800"
        >
          <p className="font-semibold">{submitError}</p>
          {errorList.length > 0 && (
            <ul className="mt-2 list-disc space-y-1 pl-5">
              {errorList.map((e) => (
                <li key={e.field}>
                  <span className="font-semibold">{e.field}:</span> {e.message}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      <div>
        <label htmlFor="fullName" className="field-label">
          Full name
        </label>
        <input
          id="fullName"
          type="text"
          autoComplete="name"
          placeholder="e.g. Khalid Al-Otaibi"
          className={clsx("field-input", errors.fullName && "border-red-400")}
          {...register("fullName")}
        />
        {errors.fullName && (
          <p className="field-error" role="alert">
            {errors.fullName.message}
          </p>
        )}
      </div>

      <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
        <div>
          <label htmlFor="dateOfBirth" className="field-label">
            Date of birth
          </label>
          <input
            id="dateOfBirth"
            type="date"
            max={todayIso}
            className={clsx("field-input", errors.dateOfBirth && "border-red-400")}
            {...register("dateOfBirth")}
          />
          {errors.dateOfBirth ? (
            <p className="field-error" role="alert">
              {errors.dateOfBirth.message}
            </p>
          ) : Number.isFinite(age) ? (
            <p className="mt-1 text-xs text-slate-500">Age: {age}</p>
          ) : null}
        </div>

        <div>
          <label htmlFor="role" className="field-label">
            Role
          </label>
          <select
            id="role"
            className={clsx("field-input", errors.role && "border-red-400")}
            {...register("role")}
          >
            <option value={MemberRole.Athlete}>Athlete</option>
            <option value={MemberRole.Coach}>Coach</option>
            <option value={MemberRole.Referee}>Referee</option>
          </select>
          {errors.role && (
            <p className="field-error" role="alert">
              {errors.role.message}
            </p>
          )}
        </div>
      </div>

      <fieldset className="rounded-lg border border-slate-200 p-4">
        <legend className="px-1 text-sm font-semibold text-slate-700">
          Contact & identity
        </legend>
        <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
          <div>
            <label htmlFor="email" className="field-label">
              Email
            </label>
            <input
              id="email"
              type="email"
              autoComplete="email"
              placeholder="name@example.com"
              className={clsx("field-input", errors.email && "border-red-400")}
              {...register("email")}
            />
            {errors.email && (
              <p className="field-error" role="alert">
                {errors.email.message}
              </p>
            )}
          </div>

          <div>
            <label htmlFor="phoneNumber" className="field-label">
              Phone number
            </label>
            <input
              id="phoneNumber"
              type="tel"
              autoComplete="tel"
              placeholder="+966 5X XXX XXXX"
              className={clsx("field-input", errors.phoneNumber && "border-red-400")}
              {...register("phoneNumber")}
            />
            {errors.phoneNumber && (
              <p className="field-error" role="alert">
                {errors.phoneNumber.message}
              </p>
            )}
          </div>

          <div className="sm:col-span-2">
            <label htmlFor="nationalId" className="field-label">
              National ID / Iqama / Passport
            </label>
            <input
              id="nationalId"
              type="text"
              autoComplete="off"
              placeholder="10-digit Saudi ID or international passport number"
              className={clsx("field-input", errors.nationalId && "border-red-400")}
              {...register("nationalId")}
            />
            {errors.nationalId && (
              <p className="field-error" role="alert">
                {errors.nationalId.message}
              </p>
            )}
          </div>
        </div>
      </fieldset>

      {showGuardianConsent && (
        <fieldset className="rounded-lg border border-amber-300 bg-amber-50 p-4">
          <legend className="px-1 text-sm font-semibold text-amber-900">Parental consent</legend>
          <p className="mb-3 text-xs text-amber-800">
            This athlete is under 18. We'll email a guardian a secure link to digitally sign the registration.
            The link expires in 7 days. If the guardian has already signed in person at the federation office,
            tick the in-person box at the bottom instead.
          </p>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label htmlFor="guardianFullName" className="field-label">Guardian full name</label>
              <input
                id="guardianFullName"
                type="text"
                className={clsx("field-input", errors.guardianFullName && "border-red-400")}
                {...register("guardianFullName")}
              />
              {errors.guardianFullName && (
                <p className="field-error" role="alert">{errors.guardianFullName.message}</p>
              )}
            </div>

            <div>
              <label htmlFor="guardianRelation" className="field-label">Relation</label>
              <select
                id="guardianRelation"
                className={clsx("field-input", errors.guardianRelation && "border-red-400")}
                {...register("guardianRelation", { required: false })}
              >
                <option value="" disabled>
                  — Select relation —
                </option>
                {RELATIONS.map((r) => (
                  <option key={r} value={r}>
                    {r === "LegalGuardian" ? "Legal guardian" : r}
                  </option>
                ))}
              </select>
              {errors.guardianRelation && (
                <p className="field-error" role="alert">{errors.guardianRelation.message}</p>
              )}
            </div>

            <div>
              <label htmlFor="guardianEmail" className="field-label">Guardian email</label>
              <input
                id="guardianEmail"
                type="email"
                className={clsx("field-input", errors.guardianEmail && "border-red-400")}
                {...register("guardianEmail")}
              />
              {errors.guardianEmail && (
                <p className="field-error" role="alert">{errors.guardianEmail.message}</p>
              )}
            </div>

            <div>
              <label htmlFor="guardianPhone" className="field-label">Guardian phone</label>
              <input
                id="guardianPhone"
                type="tel"
                placeholder="+966 5X XXX XXXX"
                className={clsx("field-input", errors.guardianPhone && "border-red-400")}
                {...register("guardianPhone")}
              />
              {errors.guardianPhone && (
                <p className="field-error" role="alert">{errors.guardianPhone.message}</p>
              )}
            </div>

            <div className="sm:col-span-2">
              <label htmlFor="guardianNationalId" className="field-label">Guardian national ID (optional)</label>
              <input
                id="guardianNationalId"
                type="text"
                className="field-input"
                {...register("guardianNationalId")}
              />
            </div>
          </div>

          <div className="mt-3 flex items-start gap-3 border-t border-amber-200 pt-3">
            <input
              id="guardianConsent"
              type="checkbox"
              className="mt-1 h-4 w-4 rounded border-slate-300 text-smf-600 focus:ring-smf-500"
              {...register("guardianConsent")}
            />
            <label htmlFor="guardianConsent" className="text-xs text-amber-900">
              <span className="font-semibold">In-person fast path:</span> the guardian has already signed
              the registration form on paper at the federation office.
            </label>
          </div>
        </fieldset>
      )}

      <fieldset className="rounded-lg border border-slate-200 p-4">
        <legend className="px-1 text-sm font-semibold text-slate-700">
          Compliance
        </legend>
        <p className="mb-3 text-xs text-slate-500">
          Required. Your acceptance is recorded with a timestamp for governance
          auditing (SOPC / IFMA standards).
        </p>
        <div className="space-y-3">
          <ComplianceCheckbox
            id="acceptTerms"
            label="I accept the federation's Terms of Service."
            error={errors.acceptTerms?.message}
            registration={register("acceptTerms")}
          />
          <ComplianceCheckbox
            id="acceptPrivacyPolicy"
            label="I accept the Privacy Policy (GDPR- and KSA-compliant data handling)."
            error={errors.acceptPrivacyPolicy?.message}
            registration={register("acceptPrivacyPolicy")}
          />
          <ComplianceCheckbox
            id="acceptCodeOfConduct"
            label="I accept the Code of Conduct (fair play, discipline, anti-doping)."
            error={errors.acceptCodeOfConduct?.message}
            registration={register("acceptCodeOfConduct")}
          />
        </div>
      </fieldset>

      {result && (
        <div
          role="status"
          className="space-y-2 rounded-lg border border-smf-500 bg-smf-50 px-4 py-3 text-sm text-smf-700"
        >
          <p>
            Registered successfully — your SMF ID is{" "}
            <span className="font-semibold">{result.smF_ID}</span>. Status:{" "}
            {result.registrationStatus}.
          </p>
          {result.parentalConsentId && (
            <p className="rounded-md border border-amber-300 bg-amber-50 px-3 py-2 text-xs text-amber-900">
              We've emailed a secure consent link to the guardian.
              {result.parentalConsentExpiresAtUtc && (
                <> It expires on{" "}
                  <span className="font-mono">
                    {new Date(result.parentalConsentExpiresAtUtc).toLocaleString()}
                  </span>.</>
              )}
              {" "}Registration becomes active once the guardian signs.
            </p>
          )}
        </div>
      )}

      <button type="submit" disabled={isSubmitting} className="btn-primary">
        {isSubmitting ? "Submitting…" : "Register"}
      </button>
    </form>
  );
}

interface ComplianceCheckboxProps {
  id: keyof RegisterMemberInput;
  label: string;
  error?: string;
  registration: UseFormRegisterReturn;
}

function ComplianceCheckbox({
  id,
  label,
  error,
  registration,
}: ComplianceCheckboxProps) {
  return (
    <div>
      <label className="flex items-start gap-3 text-sm text-slate-700">
        <input
          id={id}
          type="checkbox"
          className="mt-0.5 h-4 w-4 rounded border-slate-300 text-smf-600 focus:ring-smf-500"
          {...registration}
        />
        <span>{label}</span>
      </label>
      {error && (
        <p className="field-error ml-7" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
